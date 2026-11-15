/* =====================================================================
   02 - טבלאות הנתונים העסקיים (dbo)
   מבנה לדוגמה: סניף ← מחלקה ← עובד, ומחלקה ← פרויקט.
   בכוונה אין כאן CHECK constraints עסקיים (למשל "תאריך סיום אחרי תאריך התחלה"):
   זה מדמה מצב אמיתי של נתונים שמגיעים ממערכות שונות, ובדיוק בגלל זה צריך בקרות.
   ===================================================================== */
USE DataControlsDemo;
GO

CREATE TABLE dbo.Branches
(
    BranchId    INT IDENTITY(1,1) CONSTRAINT PK_Branches PRIMARY KEY,
    BranchName  NVARCHAR(100) NOT NULL,
    City        NVARCHAR(100) NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1)
);
GO

CREATE TABLE dbo.Departments
(
    DepartmentId    INT IDENTITY(1,1) CONSTRAINT PK_Departments PRIMARY KEY,
    DepartmentName  NVARCHAR(100) NOT NULL,
    BranchId        INT NULL CONSTRAINT FK_Departments_Branches REFERENCES dbo.Branches(BranchId),
    Budget          DECIMAL(14,2) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT (1)
);
GO

CREATE TABLE dbo.Employees
(
    EmployeeId          INT IDENTITY(1,1) CONSTRAINT PK_Employees PRIMARY KEY,
    IdNumber            CHAR(9) NULL,
    FirstName           NVARCHAR(50) NOT NULL,
    LastName            NVARCHAR(50) NOT NULL,
    BirthDate           DATE NULL,
    -- עמודה מחושבת: גיל מדויק נכון להיום (לא נשמר פיזית, מחושב בכל שליפה)
    Age AS (
        DATEDIFF(YEAR, BirthDate, CAST(GETDATE() AS DATE))
        - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, BirthDate, CAST(GETDATE() AS DATE)), BirthDate) > CAST(GETDATE() AS DATE)
               THEN 1 ELSE 0 END
    ),
    Email               NVARCHAR(200) NULL,
    Phone               VARCHAR(20) NULL,
    Salary              DECIMAL(10,2) NULL,
    EmploymentStartDate DATE NULL,
    EmploymentEndDate   DATE NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1),
    DepartmentId        INT NULL CONSTRAINT FK_Employees_Departments REFERENCES dbo.Departments(DepartmentId)
);
GO

CREATE TABLE dbo.Projects
(
    ProjectId     INT IDENTITY(1,1) CONSTRAINT PK_Projects PRIMARY KEY,
    ProjectName   NVARCHAR(150) NOT NULL,
    DepartmentId  INT NULL CONSTRAINT FK_Projects_Departments REFERENCES dbo.Departments(DepartmentId),
    Budget        DECIMAL(14,2) NULL,
    StartDate     DATE NULL,
    EndDate       DATE NULL,
    Status        VARCHAR(20) NOT NULL CONSTRAINT DF_Projects_Status DEFAULT ('Planned')
);
GO

-- אינדקסים על מפתחות זרים: ה-JOIN-ים שהמנוע מייצר עוברים דרכם
CREATE INDEX IX_Employees_DepartmentId ON dbo.Employees(DepartmentId);
CREATE INDEX IX_Departments_BranchId   ON dbo.Departments(BranchId);
CREATE INDEX IX_Projects_DepartmentId  ON dbo.Projects(DepartmentId);
GO

PRINT N'✔ Business tables created';
GO
