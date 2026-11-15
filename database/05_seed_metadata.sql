/* =====================================================================
   05 - מילוי השכבה הסמנטית
   כאן "מלמדים" את המערכת על הנתונים, במונחים עסקיים.
   להוספת טבלה חדשה לבקרות: מוסיפים ישות + שדות + קשרים כאן. אפס שינויי קוד.
   ===================================================================== */
USE DataControlsDemo;
GO

-- ---------- ישויות ----------
INSERT INTO meta.Entities (EntityKey, DisplayName, DisplayNamePlural, Description, SchemaName, TableName, PrimaryKeyColumn, TitleFieldKeys, Synonyms) VALUES
 ('Employee',   N'עובד',    N'עובדים',   N'עובדי הארגון, כולל עובדים שסיימו את העסקתם', 'dbo', 'Employees',   'EmployeeId',   'firstName,lastName', N'עובד,עובדים,עובדת,עובדות,מועסק,מועסקים,איש צוות'),
 ('Department', N'מחלקה',   N'מחלקות',   N'היחידות הארגוניות',                           'dbo', 'Departments', 'DepartmentId', 'name',               N'מחלקה,מחלקות,יחידה,יחידות'),
 ('Branch',     N'סניף',    N'סניפים',   N'הסניפים הגאוגרפיים של הארגון',                 'dbo', 'Branches',    'BranchId',     'name',               N'סניף,סניפים,אתר,אתרים'),
 ('Project',    N'פרויקט',  N'פרויקטים', N'פרויקטים בניהול המחלקות',                      'dbo', 'Projects',    'ProjectId',    'name',               N'פרויקט,פרויקטים,פרוייקט,פרוייקטים');
GO

DECLARE @Emp INT  = (SELECT EntityId FROM meta.Entities WHERE EntityKey = 'Employee');
DECLARE @Dep INT  = (SELECT EntityId FROM meta.Entities WHERE EntityKey = 'Department');
DECLARE @Br  INT  = (SELECT EntityId FROM meta.Entities WHERE EntityKey = 'Branch');
DECLARE @Prj INT  = (SELECT EntityId FROM meta.Entities WHERE EntityKey = 'Project');

-- ---------- שדות ----------
INSERT INTO meta.Fields (EntityId, FieldKey, DisplayName, Description, ColumnName, DataType, Unit, Synonyms, ShowInResults, SortOrder) VALUES
 (@Emp, 'idNumber',      N'תעודת זהות',          NULL,                                   'IdNumber',            'string',  NULL,     N'ת"ז,תז,מספר זהות',                1, 1),
 (@Emp, 'firstName',     N'שם פרטי',             NULL,                                   'FirstName',           'string',  NULL,     NULL,                               1, 2),
 (@Emp, 'lastName',      N'שם משפחה',            NULL,                                   'LastName',            'string',  NULL,     NULL,                               1, 3),
 (@Emp, 'birthDate',     N'תאריך לידה',          NULL,                                   'BirthDate',           'date',    NULL,     NULL,                               1, 4),
 (@Emp, 'age',           N'גיל',                 N'מחושב אוטומטית מתאריך הלידה',          'Age',                 'number',  N'שנים',  N'גילו,גילה,גילם,בן,בת',            1, 5),
 (@Emp, 'email',         N'כתובת אימייל',        NULL,                                   'Email',               'string',  NULL,     N'אימייל,מייל,דוא"ל,דואר אלקטרוני', 1, 6),
 (@Emp, 'phone',         N'טלפון',               NULL,                                   'Phone',               'string',  NULL,     N'נייד,פלאפון,מספר טלפון',          0, 7),
 (@Emp, 'salary',        N'שכר חודשי',           N'שכר ברוטו חודשי',                     'Salary',              'number',  N'ש"ח',   N'שכר,משכורת,משכורתו,משכורתם,שכרו,שכרם,תשלום', 1, 8),
 (@Emp, 'startDate',     N'תאריך תחילת העסקה',   NULL,                                   'EmploymentStartDate', 'date',    NULL,     N'תחילת העסקה,תאריך התחלה,תחילת עבודה', 1, 9),
 (@Emp, 'endDate',       N'תאריך סיום העסקה',    N'ריק לעובד שעדיין מועסק',              'EmploymentEndDate',   'date',    NULL,     N'סיום העסקה,תאריך סיום,סיום עבודה',    1, 10),
 (@Emp, 'isActive',      N'עובד פעיל',           NULL,                                   'IsActive',            'boolean', NULL,     N'פעיל,פעילים,פעילה',               1, 11),

 (@Dep, 'name',          N'שם המחלקה',           NULL,                                   'DepartmentName',      'string',  NULL,     NULL,                               1, 1),
 (@Dep, 'budget',        N'תקציב שנתי',          NULL,                                   'Budget',              'number',  N'ש"ח',   N'תקציב',                           1, 2),
 (@Dep, 'isActive',      N'מחלקה פעילה',         NULL,                                   'IsActive',            'boolean', NULL,     N'פעילה,פעילות',                    1, 3),

 (@Br,  'name',          N'שם הסניף',            NULL,                                   'BranchName',          'string',  NULL,     NULL,                               1, 1),
 (@Br,  'city',          N'עיר',                 NULL,                                   'City',                'string',  NULL,     N'ישוב,מיקום',                      1, 2),
 (@Br,  'isActive',      N'סניף פעיל',           NULL,                                   'IsActive',            'boolean', NULL,     N'פעיל,פתוח',                       1, 3),

 (@Prj, 'name',          N'שם הפרויקט',          NULL,                                   'ProjectName',         'string',  NULL,     NULL,                               1, 1),
 (@Prj, 'budget',        N'תקציב הפרויקט',       NULL,                                   'Budget',              'number',  N'ש"ח',   N'תקציב',                           1, 2),
 (@Prj, 'startDate',     N'תאריך התחלה',         NULL,                                   'StartDate',           'date',    NULL,     N'התחלה',                           1, 3),
 (@Prj, 'endDate',       N'תאריך סיום',          NULL,                                   'EndDate',             'date',    NULL,     N'סיום',                            1, 4),
 (@Prj, 'status',        N'סטטוס',               NULL,                                   'Status',              'string',  NULL,     N'מצב,סטטוס',                       1, 5);

-- ---------- ערכים אפשריים (רשימה נפתחת) ----------
INSERT INTO meta.FieldOptions (FieldId, Value, Label, SortOrder)
SELECT f.FieldId, v.Value, v.Label, v.SortOrder
FROM meta.Fields f
CROSS JOIN (VALUES ('Planned', N'מתוכנן', 1), ('Active', N'פעיל', 2), ('Closed', N'סגור', 3)) v(Value, Label, SortOrder)
WHERE f.EntityId = @Prj AND f.FieldKey = 'status';

-- ---------- קשרים ----------
INSERT INTO meta.Relations (FromEntityId, ToEntityId, NavigationKey, DisplayName, FromColumn, ToColumn) VALUES
 (@Emp, @Dep, 'department', N'המחלקה של העובד',   'DepartmentId', 'DepartmentId'),
 (@Dep, @Br,  'branch',     N'הסניף של המחלקה',   'BranchId',     'BranchId'),
 (@Prj, @Dep, 'department', N'המחלקה של הפרויקט', 'DepartmentId', 'DepartmentId');
GO

PRINT N'✔ Semantic metadata loaded';
GO
