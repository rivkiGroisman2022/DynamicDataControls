"""
Builds the instructions (prompt) for a language model from the data structure context.

This is the "mechanism for passing the model relevant information about the data structure and its business meaning":
- Only business terms and paths (department.isActive) - no table or column names, and no SQL.
- The system part is fixed for a given data structure (good for Prompt Caching), and the user's request comes separately.
"""

from __future__ import annotations

from app.schemas import EntityContext, SchemaContext

SYSTEM_TEMPLATE = """\
You translate business data-quality requirements written in Hebrew into a structured control definition.
Business users describe which records are problematic; you return a definition the control engine can run.

How a control works:
- A control runs on one business entity and returns the records that MATCH its condition. The condition describes the violation.
- Conditions form a tree: groups combine conditions with "and" / "or"; a rule compares one field.
- Refer to fields only by their exact path from the catalog below (e.g. "salary", "department.isActive").
  Fields of related entities are reached through their path prefix; never invent a path.
- Use only operators listed for the field. A rule may compare to a constant ("value"), a range ("value" + "valueTo"),
  a list ("values"), or another field of the same data type ("compareField").
- Dates are "YYYY-MM-DD". Today is {today}. Numbers are plain numbers (15000, not "15,000").
- Never write SQL. You only produce the definition; the engine generates and runs the query safely.

Answer guidelines:
- name: a short Hebrew title for the control. description: one Hebrew sentence.
- explanation: short Hebrew lines showing how each phrase of the request mapped to a field and operator.
- confidence: 0 to 1. Lower it when you had to assume something.
- warnings: Hebrew notes about parts of the request you could not map or interpreted with an assumption.
  If a requirement cannot be expressed with the catalog, say so in warnings instead of guessing.

Operators:
{operators}

Catalog of entities and fields (path | name | type | unit | synonyms | allowed values):
{catalog}
"""


def _entity_block(entity: EntityContext) -> str:
    lines = [f"## {entity.key} - {entity.display_name_plural} ({entity.description or ''})".rstrip(" ()")]
    for f in entity.fields:
        parts = [f.path, f.display_name, f.data_type, f.unit or "-", ", ".join(f.synonyms) or "-"]
        if f.options:
            parts.append(", ".join(f"{o.value}={o.label}" for o in f.options))
        lines.append("- " + " | ".join(parts))
    return "\n".join(lines)


def build_system_prompt(schema: SchemaContext) -> str:
    operators = "\n".join(
        f"- {o.key}: {o.label} (types: {', '.join(o.data_types)}; value: {o.arity}"
        f"{'; can compare to another field' if o.supports_field_comparison else ''})"
        for o in schema.operators
    )
    catalog = "\n\n".join(_entity_block(e) for e in schema.entities)
    return SYSTEM_TEMPLATE.format(today=schema.today, operators=operators, catalog=catalog)


def build_user_message(text: str) -> str:
    return f"The business requirement (Hebrew):\n<requirement>\n{text.strip()}\n</requirement>"


def build_quality_message(entity: str | None) -> str:
    scope = f'the entity "{entity}"' if entity else "every entity in the catalog"
    return (
        f"Analyze the structure of {scope} and propose up to 6 data-quality controls a business would want: "
        "inconsistent dates, impossible values, required data that is missing, active records linked to inactive ones. "
        "Use only fields from the catalog."
    )
