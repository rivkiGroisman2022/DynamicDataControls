"""
Mock provider - simulates a language model without any external call.

A deterministic, rule-based Hebrew parser:
1. Identifies the entity (עובדים / מחלקות ...) from names and synonyms in the metadata.
2. Finds field mentions ("גילם", "משכורתם", "תאריך סיום ההעסקה") - including fields of related entities,
   choosing among identical candidates by the "context entity" (the last entity mentioned before the word).
3. For each field, identifies an operator and value from the surrounding text ("נמוך מ 18", "ללא", "שאינה פעילה", "מוקדם מ...").
4. Joins with "and" / "or" according to the connecting words, and returns exactly the same contract a real model would return.

It relies only on the metadata (names, synonyms, types) - nothing about specific tables is hard-coded.
That is exactly what makes it a fair "stand-in" for a model: give it a different data structure and it keeps working.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from app.hebrew_text import HEB, Span, find_all, has_word, normalize
from app.providers.base import AiProvider, AiUnderstandingError
from app.quality_advisor import suggest_quality_controls
from app.schemas import (
    ConditionGroup,
    ConditionNode,
    ConditionRule,
    ControlDefinition,
    ControlSuggestion,
    EntityContext,
    FieldContext,
    JsonValue,
    SchemaContext,
)

NUM = r"(-?\d+(?:\.\d+)?)"
DATE = r"(\d{4}-\d{2}-\d{2}|\d{1,2}[/.]\d{1,2}[/.]\d{4})"

# Operators for numbers. Order matters: "greater or equal" before "greater".
NUMBER_PATTERNS: list[tuple[str, str]] = [
    ("between", rf"בין\s+{NUM}\s+(?:ל|לבין|עד)\s*{NUM}"),
    ("greaterOrEqual", rf"(?:לפחות|מינימום|(?:גדול|גבוה)\S*\s+או\s+שווה)\s*(?:ל|מ)?\s*{NUM}"),
    ("lessOrEqual", rf"(?:לכל\s+היותר|מקסימום|(?:קטן|נמוכ|נמוך)\S*\s+או\s+שווה|(?<![{HEB}])עד)\s*(?:ל)?\s*{NUM}"),
    ("greaterThan", rf"(?:גבוה\S*|גדול\S*|יותר|מעל|עולה\S*\s+על)\s*(?:מ|על|ל)?\s*{NUM}"),
    ("lessThan", rf"(?:נמוכ\S*|נמוך|קטנ\S*|קטן|פחות|מתחת)\s*(?:מ|ל)?\s*{NUM}"),
    ("equals", rf"(?:שווה\S*|בדיוק|בגיל)\s*(?:ל)?\s*{NUM}"),
]
NEGATIVE_WORDS = r"שלילי\S*"
POSITIVE_WORDS = r"חיובי\S*"
EMPTY_BEFORE = r"(?:ללא|בלי|חסר\S*|אין|לא\s+הוזנ?\S*|ריק\S*)\s*$"
EMPTY_AFTER = r"^\s*(?:\S+\s+){0,2}?(?:חסר\S*|ריק\S*|לא\s+הוזנ?\S*|לא\s+מולא\S*)"
NEGATION_BEFORE = r"(?:לא|אינ\S*|שאינ\S*|אין|שלא)\s*$"
CLOSED_WORDS = r"סגור\S*|מושבת\S*|מבוטל\S*|לא\s+פעיל\S*"
EARLIER = r"(?:מוקדם\S*|לפני|קודם)"
LATER = r"(?:מאוחר\S*|אחרי)"
PAST = r"(?:כבר\s+)?(?:עבר|חלף|שעבר|שחלף|בעבר)"
FUTURE = r"(?:עתידי\S*|בעתיד|טרם\s+הגיע)"
OR_WORD = rf"(?<![{HEB}])או(?![{HEB}])"
LEADING_VERBS = r"^(?:מצא|מצאי|תמצא|איתור|אתר|הצג|הציגי|חפש|רשימת|תן\s+לי|אני\s+רוצה)\s+"


@dataclass
class Mention:
    """A mention of a field in the text."""
    span: Span
    field: FieldContext
    #: An option value of the field (e.g. "פעיל" → status = Active) when the mention is the option label itself.
    option_value: str | None = None
    #: The mention is a "closed / inactive" word that refers to the context entity's active field.
    implies_false: bool = False


@dataclass
class Parsed:
    rule: ConditionRule
    mention: Mention
    explanation: str
    consumed: set[int] = field(default_factory=set)


class MockProvider(AiProvider):
    name = "mock"
    model = "rule-based-hebrew-v1"

    # ------------------------------------------------------------------ public

    def suggest_control(self, text: str, schema: SchemaContext) -> ControlSuggestion:
        clean = normalize(text)
        warnings: list[str] = []
        explanation: list[str] = []

        entity, entity_phrase = self._detect_entity(clean, schema)
        if entity is None:
            raise AiUnderstandingError("לא זוהו בתיאור הנתונים שעליהם תרוץ הבקרה (למשל: עובדים, מחלקות).")
        if entity_phrase:
            explanation.append(f'נתונים: {entity.display_name_plural} (זוהה מהמילה "{entity_phrase}")')
        else:
            warnings.append(f"לא צוין במפורש על אילו נתונים הבקרה. הנחתי: {entity.display_name_plural}.")

        mentions = self._find_mentions(clean, entity, schema)
        parsed = self._parse_conditions(clean, mentions, entity, warnings)
        if not parsed:
            raise AiUnderstandingError("לא זוהו תנאים בתיאור. נסו לציין שדה ותנאי, למשל: \"שכר גבוה מ־15,000\".")

        explanation += [p.explanation for p in parsed]
        condition = self._combine(clean, parsed)
        for clause in self._unexplained_clauses(clean, mentions):
            warnings.append(f'החלק "{clause}" לא הובן ולכן לא נכלל בבקרה.')

        confidence = 0.92 - 0.12 * len(warnings) - (0.2 if not entity_phrase else 0)
        name = self._name_from(text)
        return ControlSuggestion(
            name=name,
            description=text.strip(),
            severity="high" if has_word(r"חמור\S*|קריטי\S*|דחוף\S*", clean) else "medium",
            definition=ControlDefinition(entity=entity.key, condition=condition),
            explanation=explanation,
            confidence=round(max(0.2, min(confidence, 0.95)), 2),
            warnings=warnings,
            provider=self.name,
            model=self.model,
        )

    def suggest_quality_controls(self, schema: SchemaContext, entity: str | None = None) -> list[ControlSuggestion]:
        return suggest_quality_controls(schema, entity, provider=self.name, model=self.model)

    # ------------------------------------------------------------------ entity

    @staticmethod
    def _entity_phrases(entity: EntityContext) -> list[str]:
        return [entity.display_name_plural, entity.display_name, *entity.synonyms]

    def _detect_entity(self, text: str, schema: SchemaContext) -> tuple[EntityContext | None, str | None]:
        """The entity mentioned first in the text is the one the control is "on" ("עובדים פעילים במחלקה..." → עובדים)."""
        best: tuple[int, EntityContext, str] | None = None
        for entity in schema.entities:
            for phrase in self._entity_phrases(entity):
                for span in find_all(phrase, text):
                    if best is None or span.start < best[0]:
                        best = (span.start, entity, span.text)
        if best:
            return best[1], best[2]
        # No entity mentioned: choose the one whose fields are mentioned most often.
        scored = [(len(self._find_mentions(text, e, schema)), e) for e in schema.entities]
        scored.sort(key=lambda x: -x[0])
        return (scored[0][1], None) if scored and scored[0][0] > 0 else (None, None)

    def _entity_mentions(self, text: str, schema: SchemaContext) -> list[tuple[Span, str]]:
        found = []
        for entity in schema.entities:
            for phrase in self._entity_phrases(entity):
                found += [(s, entity.key) for s in find_all(phrase, text)]
        return sorted(found, key=lambda x: x[0].start)

    # ------------------------------------------------------------------ fields

    def _find_mentions(self, text: str, entity: EntityContext, schema: SchemaContext) -> list[Mention]:
        entity_mentions = self._entity_mentions(text, schema)

        def context_entity(position: int) -> str:
            before = [key for span, key in entity_mentions if span.start <= position]
            return before[-1] if before else entity.key

        candidates: dict[tuple[int, int], list[Mention]] = {}
        for f in entity.fields:
            for phrase in [f.display_name, *f.synonyms]:
                for span in find_all(phrase, text):
                    candidates.setdefault((span.start, span.end), []).append(Mention(span, f))
            for option in f.options:
                for span in find_all(option.label, text):
                    candidates.setdefault((span.start, span.end), []).append(Mention(span, f, option_value=option.value))

        # "Closed" / "inactive" words → the context entity's active (boolean) field
        for m in re.finditer(rf"(?<![{HEB}])(?:{CLOSED_WORDS})(?![{HEB}])", text):
            key = context_entity(m.start())
            target = next((f for f in entity.fields if f.entity_key == key and f.data_type == "boolean"
                           and any("פעיל" in s for s in [f.display_name, *f.synonyms])), None)
            if target:
                span = Span(m.start(), m.end(), m.group(0))
                candidates[(span.start, span.end)] = [Mention(span, target, implies_false=True)]

        chosen: list[Mention] = []
        for (start, _), options in candidates.items():
            ctx = context_entity(start)
            # Preference: a field of the context entity; otherwise a field of the base entity; otherwise the shortest path.
            options.sort(key=lambda m: (m.field.entity_key != ctx, m.field.entity_key != entity.key,
                                        m.option_value is not None, m.field.depth))
            chosen.append(options[0])

        # Overlapping mentions: the longest wins ("תאריך סיום העסקה" beats "תאריך").
        chosen.sort(key=lambda m: (-(m.span.end - m.span.start), m.span.start))
        result: list[Mention] = []
        for m in chosen:
            if all(m.span.end <= r.span.start or m.span.start >= r.span.end for r in result):
                result.append(m)
        return sorted(result, key=lambda m: m.span.start)

    # ------------------------------------------------------------------ conditions

    def _parse_conditions(self, text: str, mentions: list[Mention], entity: EntityContext,
                          warnings: list[str]) -> list[Parsed]:
        parsed: list[Parsed] = []
        skip: set[int] = set()

        for i, mention in enumerate(mentions):
            if i in skip:
                continue
            prev_end = mentions[i - 1].span.end if i > 0 else 0
            next_start = mentions[i + 1].span.start if i + 1 < len(mentions) else len(text)
            before = text[prev_end:mention.span.start]
            after = text[mention.span.end:next_start]
            following = mentions[i + 1] if i + 1 < len(mentions) else None

            result = self._parse_one(mention, before, after, following, entity)
            if result is None:
                warnings.append(f'לא הבנתי איזה תנאי חל על "{mention.field.display_name}" ולכן הוא לא נכלל.')
                continue
            if result.rule.compare_field:
                skip.add(i + 1)
            parsed.append(result)
        return parsed

    def _parse_one(self, m: Mention, before: str, after: str, following: Mention | None,
                   entity: EntityContext) -> Parsed | None:
        f = m.field
        said = m.span.text

        def done(operator: str, *, value: JsonValue = None, value_to: JsonValue = None,
                 compare: FieldContext | None = None, how: str) -> Parsed:
            rule = ConditionRule(field=f.path, operator=operator, value=value, value_to=value_to,
                                 compare_field=compare.path if compare else None)
            where = f" ({f.group_label})" if f.depth else ""
            return Parsed(rule, m, f'"{said}" ← {f.display_name}{where}: {how}')

        # Empty / missing - for any type ("ללא אימייל", "אימייל חסר")
        if re.search(EMPTY_BEFORE, before) or re.search(EMPTY_AFTER, after):
            return done("isEmpty", how="ריק / חסר")

        if m.implies_false:
            return done("isFalse", how="לא (סגור / לא פעיל)")

        if m.option_value is not None:
            return done("equals", value=m.option_value, how=f'שווה ל"{said}"')

        if f.data_type == "boolean":
            negated = bool(re.search(NEGATION_BEFORE, before))
            return done("isFalse" if negated else "isTrue", how="לא" if negated else "כן")

        if f.data_type == "number":
            if has_word(NEGATIVE_WORDS, after):
                return done("lessThan", value=0, how="ערך שלילי (קטן מ־0)")
            if has_word(POSITIVE_WORDS, after):
                return done("greaterThan", value=0, how="ערך חיובי (גדול מ־0)")
            for operator, pattern in NUMBER_PATTERNS:
                match = re.search(pattern, after)
                if match:
                    numbers = [self._number(g) for g in match.groups()]
                    if operator == "between":
                        return done(operator, value=numbers[0], value_to=numbers[1],
                                    how=f"בין {numbers[0]:g} ל־{numbers[1]:g}")
                    return done(operator, value=numbers[0], how=f"{self._label(operator)}{numbers[0]:g}")
            return None

        if f.data_type == "date":
            if has_word(PAST, after):
                return done("isInPast", how="תאריך שכבר עבר")
            if has_word(FUTURE, after):
                return done("isInFuture", how="תאריך עתידי")
            for operator, words in (("lessThan", EARLIER), ("greaterThan", LATER)):
                if re.search(words, after):
                    date = re.search(DATE, after)
                    if date:
                        return done(operator, value=self._date(date.group(1)),
                                    how=f"{'מוקדם' if operator == 'lessThan' else 'מאוחר'} מ־{date.group(1)}")
                    if following and following.field.data_type == "date":
                        return done(operator, compare=following.field,
                                    how=f"{'מוקדם' if operator == 'lessThan' else 'מאוחר'} מ{following.field.display_name}")
            return None

        # String: quoted text + "מכיל" / "מתחיל ב"
        quoted = re.search(r'["\']([^"\']+)["\']', after)
        if quoted:
            value = quoted.group(1)
            if has_word(r"מתחיל\S*", after):
                return done("startsWith", value=value, how=f'מתחיל ב"{value}"')
            if has_word(r"מכיל\S*|כולל\S*", after):
                return done("contains", value=value, how=f'מכיל "{value}"')
            return done("equals", value=value, how=f'שווה ל"{value}"')
        return None

    @staticmethod
    def _unexplained_clauses(text: str, mentions: list[Mention]) -> list[str]:
        """
        Clauses that no field mention covers - so their meaning would be silently lost.
        A clause starts at a connector: a comma, "וגם", "או" or a "וש..." word ("ושהם נולדו ביום שלישי").
        The first clause is skipped: it usually names the entity ("מצא עובדים").
        Reporting them as warnings (instead of ignoring them) keeps the rule "never guess, always tell".
        """
        starts = [m.start() for m in re.finditer(rf"(?:,|(?<![{HEB}])(?:וגם|או|וש[{HEB}]+)(?![{HEB}]))", text)]
        bounds = [*starts, len(text)]
        result = []
        for start, end in zip(bounds, bounds[1:]):
            if any(m.span.start < end and m.span.end > start for m in mentions):
                continue
            clause = re.sub(rf"^(?:,|וגם|או)\s*", "", text[start:end].strip()).strip(" .,")
            if re.search(rf"[{HEB}]{{2,}}", clause):
                result.append(clause)
        return result

    # ------------------------------------------------------------------ logic

    @staticmethod
    def _combine(text: str, parsed: list[Parsed]) -> ConditionGroup:
        """
        Joins conditions: "או" between two adjacent conditions → the same OR group; otherwise AND.
        Example: "פעילים ללא אימייל או ללא ת"ז" → פעיל AND (ללא אימייל OR ללא ת"ז).
        """
        chunks: list[list[ConditionRule]] = [[parsed[0].rule]]
        for prev, cur in zip(parsed, parsed[1:]):
            between = text[prev.mention.span.end:cur.mention.span.start]
            if re.search(OR_WORD, between):
                chunks[-1].append(cur.rule)
            else:
                chunks.append([cur.rule])

        nodes: list[ConditionNode] = [
            chunk[0] if len(chunk) == 1 else ConditionGroup(logic="or", conditions=list(chunk)) for chunk in chunks
        ]
        if len(nodes) == 1 and isinstance(nodes[0], ConditionGroup):
            return nodes[0]
        return ConditionGroup(logic="and", conditions=nodes)

    # ------------------------------------------------------------------ helpers

    @staticmethod
    def _number(raw: str) -> int | float:
        value = float(raw)
        return int(value) if value.is_integer() else value

    @staticmethod
    def _date(raw: str) -> str:
        if "-" in raw:
            return raw
        day, month, year = re.split(r"[/.]", raw)
        return f"{int(year):04d}-{int(month):02d}-{int(day):02d}"

    @staticmethod
    def _label(operator: str) -> str:
        return {
            "greaterThan": "גדול מ־", "lessThan": "קטן מ־", "greaterOrEqual": "לפחות ",
            "lessOrEqual": "לכל היותר ", "equals": "שווה ל־",
        }[operator]

    @staticmethod
    def _name_from(text: str) -> str:
        name = re.sub(LEADING_VERBS, "", text.strip()).rstrip(".!? ")
        name = name[:1].upper() + name[1:] if name else "בקרה חדשה"
        return name if len(name) <= 80 else name[:77].rstrip() + "..."
