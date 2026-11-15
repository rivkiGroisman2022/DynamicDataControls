"""
Advanced (optional) task: proactive data-quality control suggestions from analyzing the data structure.

Generic heuristics - based on field types and names in the metadata, not on specific tables:
1. A pair of start/end dates          → "end date earlier than start date".
2. A monetary / quantity field        → "negative value".
3. An active entity linked to an entity that has an "active" field → "active record linked to an inactive one".
4. An active entity with an end date  → "active although the end date has passed".
5. Identity / contact fields          → "active record with missing identity / contact details".
A real model (Claude) receives the same structure and can suggest more nuanced controls - this is the "base".
"""

from __future__ import annotations

import re

from app.schemas import (
    ConditionGroup,
    ConditionRule,
    ControlDefinition,
    ControlSuggestion,
    EntityContext,
    FieldContext,
    SchemaContext,
)

START = re.compile(r"start|begin|התח|תחיל", re.I)
END = re.compile(r"end|finish|סיום|סוף", re.I)
MONEY = re.compile(r'salary|budget|price|amount|cost|שכר|תקציב|מחיר|סכום|עלות|ש"ח', re.I)
REQUIRED = re.compile(r'idnumber|email|phone|זהות|ת"ז|אימייל|מייל|טלפון', re.I)


def _is_active_flag(f: FieldContext) -> bool:
    return f.data_type == "boolean" and any("פעיל" in s for s in [f.display_name, *f.synonyms])


def _text(f: FieldContext) -> str:
    return " ".join([f.path.split(".")[-1], f.display_name, *f.synonyms, f.unit or ""])


def _for_entity(e: EntityContext, provider: str, model: str | None) -> list[ControlSuggestion]:
    own = [f for f in e.fields if f.depth == 0]
    active = next((f for f in own if _is_active_flag(f)), None)
    out: list[ControlSuggestion] = []

    def add(name: str, description: str, severity: str, condition: ConditionGroup, why: str, confidence: float):
        out.append(ControlSuggestion(
            name=name, description=description, severity=severity,  # type: ignore[arg-type]
            definition=ControlDefinition(entity=e.key, condition=condition),
            explanation=[why], confidence=confidence, provider=provider, model=model,
        ))

    dates = [f for f in own if f.data_type == "date"]
    for start in (d for d in dates if START.search(_text(d))):
        for end in (d for d in dates if d is not start and END.search(_text(d))):
            add(f"{e.display_name_plural}: {end.display_name} לפני {start.display_name}",
                f"{e.display_name_plural} שבהם {end.display_name} מוקדם מ{start.display_name}", "high",
                ConditionGroup(conditions=[ConditionRule(field=end.path, operator="lessThan", compare_field=start.path)]),
                f'זוג שדות תאריך התחלה/סיום: "{start.display_name}" ו"{end.display_name}"', 0.85)

    for money in (f for f in own if f.data_type == "number" and MONEY.search(_text(f))):
        add(f"{e.display_name_plural}: {money.display_name} שלילי",
            f"{money.display_name} אינו יכול להיות שלילי", "high",
            ConditionGroup(conditions=[ConditionRule(field=money.path, operator="lessThan", value=0)]),
            f'שדה כספי/כמותי "{money.display_name}" - ערך שלילי הוא כנראה שגיאה', 0.8)

    if active:
        for related in (f for f in e.fields if f.depth == 1 and _is_active_flag(f)):
            add(f"חוסר עקביות: {active.display_name} אך לא {related.display_name}",
                f"{e.display_name_plural} פעילים המשויכים ל{related.entity_display_name} שאינה פעילה", "medium",
                ConditionGroup(conditions=[ConditionRule(field=active.path, operator="isTrue"),
                                           ConditionRule(field=related.path, operator="isFalse")]),
                f'ל{e.display_name} ול{related.entity_display_name} יש סימון "פעיל" - חוסר עקביות אפשרי בין השניים', 0.75)

        for end in (d for d in dates if END.search(_text(d))):
            add(f"חוסר עקביות: {active.display_name} אך {end.display_name} עבר",
                f"{e.display_name_plural} המסומנים פעילים אך {end.display_name} כבר חלף", "medium",
                ConditionGroup(conditions=[ConditionRule(field=active.path, operator="isTrue"),
                                           ConditionRule(field=end.path, operator="isInPast")]),
                f'"{active.display_name}" לצד "{end.display_name}" - שני מקורות לאותה עובדה', 0.7)

        required = [f for f in own if f.data_type == "string" and REQUIRED.search(_text(f))]
        if required:
            names = ", ".join(f.display_name for f in required)
            add(f"{active.display_name} עם פרטי זיהוי/קשר חסרים",
                f"ל{e.display_name} פעיל חובה: {names}", "medium",
                ConditionGroup(conditions=[
                    ConditionRule(field=active.path, operator="isTrue"),
                    ConditionGroup(logic="or", conditions=[ConditionRule(field=f.path, operator="isEmpty") for f in required]),
                ]),
                f"שדות זיהוי/קשר ({names}) הם בדרך כלל חובה לרשומה פעילה", 0.65)
    return out


def suggest_quality_controls(schema: SchemaContext, entity: str | None, provider: str,
                             model: str | None) -> list[ControlSuggestion]:
    entities = [schema.entity(entity)] if entity else schema.entities
    return [s for e in entities if e for s in _for_entity(e, provider, model)]
