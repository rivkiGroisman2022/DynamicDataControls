/* =====================================================================
   01 - יצירת בסיס הנתונים
   מוחק ויוצר מחדש את בסיס הנתונים DataControlsDemo.
   Hebrew_CI_AS - תמיכה מלאה בעברית, ללא רגישות לאותיות גדולות/קטנות.
   ===================================================================== */
USE master;
GO

IF DB_ID(N'DataControlsDemo') IS NOT NULL
BEGIN
    ALTER DATABASE DataControlsDemo SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE DataControlsDemo;
END
GO

CREATE DATABASE DataControlsDemo COLLATE Hebrew_CI_AS;
GO

USE DataControlsDemo;
GO

-- שלוש סכמות = שלוש אחריויות נפרדות
CREATE SCHEMA meta;   -- שכבה סמנטית: תרגום בין מונחים עסקיים לטבלאות ושדות
GO
CREATE SCHEMA ctl;    -- הגדרות הבקרות והיסטוריית ההרצות
GO
-- dbo = נתוני העסק עצמם (הנתונים שעליהם רצות הבקרות)

PRINT N'✔ Database DataControlsDemo created';
GO
