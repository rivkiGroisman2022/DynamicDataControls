/* =====================================================================
   06 - בקרות לדוגמה
   כל בקרה היא JSON בלבד - אין אף שאילתת SQL כתובה ידנית.
   מבנה ההגדרה (Control Definition v1):
     {
       "version": 1,
       "entity": "<EntityKey>",              -- על מה רצים
       "condition": <Group>                  -- מה נחשב חריגה
     }
     Group = { "type":"group", "logic":"and"|"or", "negate":false, "conditions":[ Group | Rule, ... ] }
     Rule  = { "type":"rule", "field":"<path>", "operator":"<op>",
               "value":..., "valueTo":..., "values":[...], "compareField":"<path>" }
     path  = שדה בישות עצמה ("salary") או דרך קשרים ("department.branch.isActive")
   רשומה "נכשלת" בבקרה כאשר היא עומדת בתנאי (התנאי מתאר את החריגה).
   ===================================================================== */
USE DataControlsDemo;
GO

INSERT INTO ctl.Controls (Name, Description, EntityKey, Severity, DefinitionJson) VALUES
(N'קטינים עם שכר גבוה',
 N'עובדים שגילם נמוך מ-18 ושכרם גבוה מ-15,000 ש"ח',
 'Employee', 'high',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"age","operator":"lessThan","value":18},
     {"type":"rule","field":"salary","operator":"greaterThan","value":15000}]}}'),

(N'סיום העסקה לפני תחילתה',
 N'עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם',
 'Employee', 'high',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"endDate","operator":"lessThan","compareField":"startDate"}]}}'),

(N'עובד פעיל במחלקה לא פעילה',
 N'עובדים פעילים המשויכים למחלקה שאינה פעילה',
 'Employee', 'medium',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"isActive","operator":"isTrue"},
     {"type":"rule","field":"department.isActive","operator":"isFalse"}]}}'),

(N'עובד פעיל עם נתוני חובה חסרים',
 N'לעובד פעיל חובה: תעודת זהות, אימייל, תאריך לידה, שכר ומחלקה',
 'Employee', 'medium',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"isActive","operator":"isTrue"},
     {"type":"group","logic":"or","conditions":[
        {"type":"rule","field":"idNumber","operator":"isEmpty"},
        {"type":"rule","field":"email","operator":"isEmpty"},
        {"type":"rule","field":"birthDate","operator":"isEmpty"},
        {"type":"rule","field":"salary","operator":"isEmpty"},
        {"type":"rule","field":"department.name","operator":"isEmpty"}]}]}}'),

(N'עובד פעיל שתאריך סיום העסקתו עבר',
 N'חוסר עקביות: העובד מסומן פעיל אך תאריך הסיום כבר חלף',
 'Employee', 'medium',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"isActive","operator":"isTrue"},
     {"type":"rule","field":"endDate","operator":"isInPast"}]}}'),

(N'שכר לא תקין',
 N'שכר שלילי או שכר גבוה במיוחד (מעל 100,000 ש"ח)',
 'Employee', 'high',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"or","conditions":[
     {"type":"rule","field":"salary","operator":"lessThan","value":0},
     {"type":"rule","field":"salary","operator":"greaterThan","value":100000}]}}'),

(N'עובד פעיל בסניף סגור',
 N'דוגמה לקשר דו-שלבי: עובד ← מחלקה ← סניף',
 'Employee', 'low',
 N'{"version":1,"entity":"Employee","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"isActive","operator":"isTrue"},
     {"type":"rule","field":"department.branch.isActive","operator":"isFalse"}]}}'),

(N'מחלקה פעילה ללא סניף תקין',
 N'מחלקות פעילות שאינן משויכות לסניף, או שהסניף שלהן סגור',
 'Department', 'low',
 N'{"version":1,"entity":"Department","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"isActive","operator":"isTrue"},
     {"type":"group","logic":"or","conditions":[
        {"type":"rule","field":"branch.name","operator":"isEmpty"},
        {"type":"rule","field":"branch.isActive","operator":"isFalse"}]}]}}'),

(N'פרויקט עם נתוני תקציב או תאריכים שגויים',
 N'תקציב שלילי, או תאריך סיום מוקדם מתאריך ההתחלה',
 'Project', 'high',
 N'{"version":1,"entity":"Project","condition":{"type":"group","logic":"or","conditions":[
     {"type":"rule","field":"budget","operator":"lessThan","value":0},
     {"type":"rule","field":"endDate","operator":"lessThan","compareField":"startDate"}]}}'),

(N'פרויקט פעיל במחלקה לא פעילה',
 N'פרויקטים בסטטוס פעיל ששייכים למחלקה שנסגרה',
 'Project', 'medium',
 N'{"version":1,"entity":"Project","condition":{"type":"group","logic":"and","conditions":[
     {"type":"rule","field":"status","operator":"equals","value":"Active"},
     {"type":"rule","field":"department.isActive","operator":"isFalse"}]}}');
GO

PRINT N'✔ Sample controls loaded';
GO
