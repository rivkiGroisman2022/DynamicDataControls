/* =====================================================================
   07 - אבטחה: משתמש מוגבל להרצת בקרות
   שכבת הגנה נוספת (Defense in Depth): גם אם תהיה תקלה במנוע יצירת השאילתות,
   השאילתה רצה בהרשאות של משתמש שיכול רק לקרוא - לא למחוק, לא לעדכן, לא לגשת ל-ctl.

   השרת מריץ כל בקרה כך:  EXECUTE AS USER = 'dc_control_runner'; <SELECT>; REVERT;
   בסביבת ייצור מומלץ בנוסף Connection String נפרד עם Login של קריאה בלבד.
   ===================================================================== */
USE DataControlsDemo;
GO

CREATE USER dc_control_runner WITHOUT LOGIN;
GO

-- קריאה בלבד: רק לטבלאות העסקיות
GRANT SELECT ON SCHEMA::dbo TO dc_control_runner;
-- איסור מפורש על כל שינוי (DENY גובר על כל GRANT אחר)
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::dbo  TO dc_control_runner;
DENY SELECT, INSERT, UPDATE, DELETE, ALTER ON SCHEMA::ctl  TO dc_control_runner;
DENY INSERT, UPDATE, DELETE, ALTER ON SCHEMA::meta TO dc_control_runner;
GO

PRINT N'✔ Read-only control runner user created';
GO
