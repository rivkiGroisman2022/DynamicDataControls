using System.Text.Json;
using DataControls.Core.Definitions;
using DataControls.Core.Metadata;

namespace DataControls.Tests;

/// <summary>An in-memory metadata catalog that mirrors the real structure. Lets the engine be tested without a database.</summary>
public static class TestData
{
    public static MetadataCatalog Catalog() => new(
    [
        new EntityMeta("Employee", "עובד", "עובדים", null, "dbo", "Employees", "EmployeeId", ["firstName", "lastName"], [], true,
            [
                Field("firstName", "שם פרטי", "FirstName", FieldDataType.String, 1),
                Field("lastName", "שם משפחה", "LastName", FieldDataType.String, 2),
                Field("age", "גיל", "Age", FieldDataType.Number, 3),
                Field("salary", "שכר חודשי", "Salary", FieldDataType.Number, 4, unit: "ש\"ח"),
                Field("startDate", "תאריך תחילת העסקה", "EmploymentStartDate", FieldDataType.Date, 5),
                Field("endDate", "תאריך סיום העסקה", "EmploymentEndDate", FieldDataType.Date, 6),
                Field("isActive", "עובד פעיל", "IsActive", FieldDataType.Boolean, 7),
                Field("email", "כתובת אימייל", "Email", FieldDataType.String, 8, show: false),
            ],
            [new RelationMeta("department", "המחלקה של העובד", "Department", "DepartmentId", "DepartmentId")]),

        new EntityMeta("Department", "מחלקה", "מחלקות", null, "dbo", "Departments", "DepartmentId", ["name"], [], true,
            [
                Field("name", "שם המחלקה", "DepartmentName", FieldDataType.String, 1),
                Field("isActive", "מחלקה פעילה", "IsActive", FieldDataType.Boolean, 2),
            ],
            [new RelationMeta("branch", "הסניף של המחלקה", "Branch", "BranchId", "BranchId")]),

        new EntityMeta("Branch", "סניף", "סניפים", null, "dbo", "Branches", "BranchId", ["name"], [], true,
            [
                Field("name", "שם הסניף", "BranchName", FieldDataType.String, 1),
                Field("isActive", "סניף פעיל", "IsActive", FieldDataType.Boolean, 2),
                Field("region", "אזור", "Region", FieldDataType.String, 3,
                    options: [new FieldOption("North", "צפון"), new FieldOption("South", "דרום")]),
            ],
            []),
    ]);

    private static FieldMeta Field(string key, string name, string column, FieldDataType type, int order,
        string? unit = null, bool show = true, FieldOption[]? options = null) =>
        new(key, name, null, column, type, unit, [], show, order, options ?? []);

    // ---------- Helpers for building definitions in tests ----------

    public static ControlDefinition Def(string entity, ConditionNode condition) =>
        new() { Entity = entity, Condition = condition };

    public static ConditionGroup And(params ConditionNode[] nodes) => new() { Logic = "and", Conditions = [.. nodes] };
    public static ConditionGroup Or(params ConditionNode[] nodes) => new() { Logic = "or", Conditions = [.. nodes] };

    public static ConditionRule Rule(string field, string op, object? value = null, object? valueTo = null,
        string? compareField = null, object[]? values = null) => new()
    {
        Field = field,
        Operator = op,
        Value = value is null ? null : Json(value),
        ValueTo = valueTo is null ? null : Json(valueTo),
        Values = values?.Select(Json).ToList(),
        CompareField = compareField
    };

    public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
}
