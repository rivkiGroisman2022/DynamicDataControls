import pytest

from app.hebrew_text import normalize
from app.providers.base import AiUnderstandingError
from app.providers.mock_provider import MockProvider
from app.schemas import ConditionGroup, ConditionRule


@pytest.fixture
def mock() -> MockProvider:
    return MockProvider()


def rules(node) -> list[tuple]:
    """Flattens the tree into (field, operator, value / compareField) for easy comparison."""
    if isinstance(node, ConditionRule):
        return [(node.field, node.operator, node.value if node.compare_field is None else "@" + node.compare_field)]
    return [r for child in node.conditions for r in rules(child)]


def test_assignment_example_minors_with_high_salary(mock, schema):
    s = mock.suggest_control('מצא עובדים פעילים שגילם נמוך מ־18 ומשכורתם גבוהה מ־15,000 ש"ח.', schema)

    assert s.definition.entity == "Employee"
    assert s.definition.condition.logic == "and"
    assert rules(s.definition.condition) == [
        ("isActive", "isTrue", None),
        ("age", "lessThan", 18),
        ("salary", "greaterThan", 15000),
    ]
    assert s.provider == "mock"
    assert s.confidence > 0.8


def test_field_to_field_date_comparison(mock, schema):
    s = mock.suggest_control("איתור עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם", schema)
    assert rules(s.definition.condition) == [("endDate", "lessThan", "@startDate")]


def test_related_entity_field_uses_context_entity(mock, schema):
    s = mock.suggest_control("עובדים פעילים המשויכים למחלקה שאינה פעילה", schema)
    assert rules(s.definition.condition) == [("isActive", "isTrue", None), ("department.isActive", "isFalse", None)]


def test_or_between_conditions_creates_nested_group(mock, schema):
    s = mock.suggest_control("עובדים פעילים ללא כתובת אימייל או ללא תעודת זהות", schema)

    root = s.definition.condition
    assert root.logic == "and"
    nested = root.conditions[1]
    assert isinstance(nested, ConditionGroup) and nested.logic == "or"
    assert rules(nested) == [("email", "isEmpty", None), ("idNumber", "isEmpty", None)]


def test_two_level_relation_and_closed_word(mock, schema):
    s = mock.suggest_control("מחלקות פעילות בסניף סגור", schema)
    assert s.definition.entity == "Department"
    assert rules(s.definition.condition) == [("isActive", "isTrue", None), ("branch.isActive", "isFalse", None)]


def test_option_label_maps_to_stored_value(mock, schema):
    s = mock.suggest_control("פרויקטים פעילים במחלקה לא פעילה", schema)
    assert rules(s.definition.condition) == [("status", "equals", "Active"), ("department.isActive", "isFalse", None)]


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("פרויקטים עם תקציב שלילי", ("budget", "lessThan", 0)),
        ("עובדים פעילים שתאריך סיום ההעסקה שלהם כבר עבר", ("endDate", "isInPast", None)),
        ("עובדים שגילם לפחות 67", ("age", "greaterOrEqual", 67)),
        ("עובדים עם שכר מעל 20 אלף", ("salary", "greaterThan", 20000)),
    ],
)
def test_operator_phrases(mock, schema, text, expected):
    assert expected in rules(mock.suggest_control(text, schema).definition.condition)


def test_range(mock, schema):
    s = mock.suggest_control("עובדים עם שכר בין 10000 ל־20000", schema)
    rule = s.definition.condition.conditions[0]
    assert (rule.operator, rule.value, rule.value_to) == ("between", 10000, 20000)


def test_explanation_shows_how_each_phrase_was_mapped(mock, schema):
    s = mock.suggest_control("עובדים שגילם נמוך מ 18", schema)
    assert any("גיל" in line and "18" in line for line in s.explanation)


def test_text_without_conditions_is_rejected(mock, schema):
    with pytest.raises(AiUnderstandingError):
        mock.suggest_control("עובדים", schema)


def test_text_without_any_known_data_is_rejected(mock, schema):
    with pytest.raises(AiUnderstandingError):
        mock.suggest_control("מה מזג האוויר מחר?", schema)


def test_unclear_condition_becomes_warning_not_guess(mock, schema):
    s = mock.suggest_control("עובדים שגילם נמוך מ 18 ושכרם מעניין", schema)
    assert rules(s.definition.condition) == [("age", "lessThan", 18)]
    assert s.warnings and s.confidence < 0.92


def test_unrecognized_clause_becomes_warning(mock, schema):
    s = mock.suggest_control("עובדים שגילם נמוך מ־18 ושהם נולדו ביום שלישי", schema)

    assert rules(s.definition.condition) == [("age", "lessThan", 18)]
    assert any("נולדו ביום שלישי" in w for w in s.warnings)
    assert s.confidence < 0.92


@pytest.mark.parametrize("text", [
    'מצא עובדים פעילים שגילם נמוך מ־18 ומשכורתם גבוהה מ־15,000 ש"ח.',
    "פרויקטים שתקציבם שלילי או שתאריך הסיום מוקדם מתאריך ההתחלה",
    "מצא עובדים, שגילם נמוך מ־18",
])
def test_fully_understood_text_has_no_warnings(mock, schema, text):
    assert mock.suggest_control(text, schema).warnings == []


def test_name_strips_leading_verb(mock, schema):
    s = mock.suggest_control("מצא עובדים שגילם נמוך מ 18", schema)
    assert s.name.startswith("עובדים")


def test_normalize_handles_maqaf_and_thousands():
    assert normalize("מ־15,000 ש״ח") == 'מ 15000 ש"ח'
