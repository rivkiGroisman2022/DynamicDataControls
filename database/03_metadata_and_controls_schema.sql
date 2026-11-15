/* =====================================================================
   03 - השכבה הסמנטית (meta) ואחסון הבקרות (ctl)

   הרעיון המרכזי של המערכת:
   הקוד לא מכיר אף טבלה או שדה. כל מה שהוא יודע על הנתונים מגיע מטבלאות meta.
   כדי לחשוף טבלה חדשה לבקרות - מוסיפים שורות ל-meta, בלי לגעת בקוד.

   meta.Entities   - "ישות עסקית"  (עובד)          ← טבלה  (dbo.Employees)
   meta.Fields     - "מאפיין עסקי" (שכר)           ← עמודה (Salary)
   meta.Relations  - "קשר עסקי"    (המחלקה של העובד) ← מפתח זר (DepartmentId)
   meta.FieldOptions - ערכים אפשריים לשדה (לתצוגת רשימה נפתחת במקום הקלדה חופשית)
   ===================================================================== */
USE DataControlsDemo;
GO

CREATE TABLE meta.Entities
(
    EntityId           INT IDENTITY(1,1) CONSTRAINT PK_meta_Entities PRIMARY KEY,
    EntityKey          VARCHAR(50)   NOT NULL CONSTRAINT UQ_meta_Entities_Key UNIQUE,  -- מזהה יציב לשימוש בהגדרות (Employee)
    DisplayName        NVARCHAR(100) NOT NULL,   -- עובד
    DisplayNamePlural  NVARCHAR(100) NOT NULL,   -- עובדים
    Description        NVARCHAR(500) NULL,
    SchemaName         SYSNAME       NOT NULL,
    TableName          SYSNAME       NOT NULL,
    PrimaryKeyColumn   SYSNAME       NOT NULL,
    TitleFieldKeys     VARCHAR(200)  NULL,       -- שדות שמרכיבים "כותרת" לרשומה בתוצאות, למשל firstName,lastName
    Synonyms           NVARCHAR(300) NULL,       -- מילים נרדפות - משמש את שכבת ה-AI
    IsControlTarget    BIT           NOT NULL CONSTRAINT DF_meta_Entities_Target DEFAULT (1)
);
GO

CREATE TABLE meta.Fields
(
    FieldId        INT IDENTITY(1,1) CONSTRAINT PK_meta_Fields PRIMARY KEY,
    EntityId       INT NOT NULL CONSTRAINT FK_meta_Fields_Entities REFERENCES meta.Entities(EntityId),
    FieldKey       VARCHAR(50)   NOT NULL,       -- salary
    DisplayName    NVARCHAR(100) NOT NULL,       -- שכר חודשי
    Description    NVARCHAR(500) NULL,
    ColumnName     SYSNAME       NOT NULL,       -- Salary
    DataType       VARCHAR(20)   NOT NULL
        CONSTRAINT CK_meta_Fields_DataType CHECK (DataType IN ('string','number','date','boolean')),
    Unit           NVARCHAR(20)  NULL,           -- ש"ח, שנים...
    Synonyms       NVARCHAR(300) NULL,           -- משכורת,שכר,תשלום
    ShowInResults  BIT NOT NULL CONSTRAINT DF_meta_Fields_Show DEFAULT (1),
    SortOrder      INT NOT NULL CONSTRAINT DF_meta_Fields_Sort DEFAULT (0),
    CONSTRAINT UQ_meta_Fields_EntityKey UNIQUE (EntityId, FieldKey)
);
GO

-- קשר "רבים-לאחד" (lookup): מהישות הנוכחית לישות שהיא מצביעה עליה
CREATE TABLE meta.Relations
(
    RelationId      INT IDENTITY(1,1) CONSTRAINT PK_meta_Relations PRIMARY KEY,
    FromEntityId    INT NOT NULL CONSTRAINT FK_meta_Relations_From REFERENCES meta.Entities(EntityId),
    ToEntityId      INT NOT NULL CONSTRAINT FK_meta_Relations_To   REFERENCES meta.Entities(EntityId),
    NavigationKey   VARCHAR(50)   NOT NULL,      -- department  (כמו Navigation Property ב-EF)
    DisplayName     NVARCHAR(100) NOT NULL,      -- המחלקה של העובד
    FromColumn      SYSNAME       NOT NULL,      -- Employees.DepartmentId
    ToColumn        SYSNAME       NOT NULL,      -- Departments.DepartmentId
    CONSTRAINT UQ_meta_Relations_Nav UNIQUE (FromEntityId, NavigationKey)
);
GO

CREATE TABLE meta.FieldOptions
(
    FieldOptionId  INT IDENTITY(1,1) CONSTRAINT PK_meta_FieldOptions PRIMARY KEY,
    FieldId        INT NOT NULL CONSTRAINT FK_meta_FieldOptions_Fields REFERENCES meta.Fields(FieldId),
    Value          NVARCHAR(100) NOT NULL,       -- Active
    Label          NVARCHAR(100) NOT NULL,       -- פעיל
    SortOrder      INT NOT NULL CONSTRAINT DF_meta_FieldOptions_Sort DEFAULT (0)
);
GO

/* ---------------------------------------------------------------------
   ctl.Controls - הגדרות הבקרות
   ההגדרה עצמה נשמרת כ-JSON (עץ תנאים). זו החלטה מכוונת:
   - עץ AND/OR בעומק לא מוגבל קשה לייצג בטבלאות רלציוניות.
   - ה-JSON הוא "החוזה" בין ה-UI, ה-AI ומנוע הבקרות.
   - ISJSON מבטיח שלא תישמר מחרוזת פגומה.
   --------------------------------------------------------------------- */
CREATE TABLE ctl.Controls
(
    ControlId       INT IDENTITY(1,1) CONSTRAINT PK_ctl_Controls PRIMARY KEY,
    Name            NVARCHAR(200) NOT NULL CONSTRAINT UQ_ctl_Controls_Name UNIQUE,
    Description     NVARCHAR(1000) NULL,
    EntityKey       VARCHAR(50)   NOT NULL,      -- על איזו ישות הבקרה רצה
    DefinitionJson  NVARCHAR(MAX) NOT NULL
        CONSTRAINT CK_ctl_Controls_Json CHECK (ISJSON(DefinitionJson) = 1),
    Severity        VARCHAR(10)   NOT NULL CONSTRAINT DF_ctl_Controls_Severity DEFAULT ('medium')
        CONSTRAINT CK_ctl_Controls_Severity CHECK (Severity IN ('low','medium','high')),
    Source          VARCHAR(10)   NOT NULL CONSTRAINT DF_ctl_Controls_Source DEFAULT ('manual')
        CONSTRAINT CK_ctl_Controls_Source CHECK (Source IN ('manual','ai')),
    IsActive        BIT           NOT NULL CONSTRAINT DF_ctl_Controls_IsActive DEFAULT (1),
    CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_ctl_Controls_Created DEFAULT (SYSDATETIME()),
    UpdatedAt       DATETIME2(0)  NULL
);
GO

-- היסטוריית הרצות: מתי, כמה חריגות, כמה זמן, האם נכשלה
CREATE TABLE ctl.ControlRuns
(
    RunId           BIGINT IDENTITY(1,1) CONSTRAINT PK_ctl_ControlRuns PRIMARY KEY,
    ControlId       INT NOT NULL CONSTRAINT FK_ctl_ControlRuns_Controls REFERENCES ctl.Controls(ControlId) ON DELETE CASCADE,
    StartedAt       DATETIME2(3) NOT NULL CONSTRAINT DF_ctl_ControlRuns_Started DEFAULT (SYSDATETIME()),
    DurationMs      INT NULL,
    Status          VARCHAR(10) NOT NULL CONSTRAINT CK_ctl_ControlRuns_Status CHECK (Status IN ('success','failed')),
    ViolationCount  INT NULL,
    ErrorMessage    NVARCHAR(2000) NULL
);
GO
CREATE INDEX IX_ctl_ControlRuns_Control ON ctl.ControlRuns(ControlId, StartedAt DESC);
GO

PRINT N'✔ Metadata and controls tables created';
GO
