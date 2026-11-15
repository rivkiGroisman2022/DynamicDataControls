<div dir="rtl">

# מערכת בקרות נתונים דינאמית

מערכת Web גנרית שמאפשרת למשתמש עסקי **להגדיר בקרות על נתונים, להריץ אותן ולנתח חריגות**, בלי לכתוב SQL ובלי להכיר את מבנה הטבלאות. בנוסף, המערכת מאפשרת לתאר בקרה **בשפה חופשית** ולקבל הצעה מוכנה מתשתית AI (בשלב זה Mock).

| רכיב | טכנולוגיה | תיקייה |
|---|---|---|
| בסיס נתונים | SQL Server | [`database/`](database) |
| שרת ראשי (API) | .NET 10 Web API, ‏Dapper | [`backend/`](backend) |
| שירות AI | Python 3, ‏FastAPI, ‏Pydantic | [`ai-service/`](ai-service) |
| צד לקוח | Angular 21, ‏Angular Material, עברית RTL | [`frontend/`](frontend) |

## מסמכים

| מסמך | תוכן |
|---|---|
| [docs/architecture.md](docs/architecture.md) | הארכיטקטורה ומנגנון הבקרות: מהגדרה, דרך שאילתה, ועד תוצאה |
| [docs/control-examples.md](docs/control-examples.md) | דוגמאות לבקרות שאפשר להגדיר ולהריץ, ואיך מוסיפים בקרה או טבלה בלי שינוי קוד |
| [docs/ai-mock-demo.md](docs/ai-mock-demo.md) | הדגמה של תהליך שלב ב' (שפה חופשית) באמצעות Mock |
| [docs/assumptions-limitations-extensions.md](docs/assumptions-limitations-extensions.md) | הנחות עבודה, מגבלות והרחבות אפשריות |
| [docs/ai-tools.md](docs/ai-tools.md) | כלי ה־AI שבהם נעשה שימוש בפיתוח, ומטרת השימוש בכל אחד |

---

## דרישות מקדימות

| כלי | גרסה שנבדקה | הערה |
|---|---|---|
| SQL Server | Express 2019 ומעלה (או כל מהדורה אחרת) | נדרשת התחברות ב־Windows Authentication |
| `sqlcmd` | מגיע עם SQL Server / SSMS | משמש את סקריפט ההקמה |
| .NET SDK | 10.0 | |
| Node.js | 22 ומעלה | כולל npm |
| Python | 3.11 ומעלה (נבדק על 3.14) | |

## התקנה והרצה

המערכת מורכבת משלושה תהליכים: API, שירות AI וצד לקוח. כל פקודה מורצת מתיקיית השורש של הפרויקט, ב־PowerShell.

### 1. הקמת בסיס הנתונים

<div dir="ltr">

```powershell
cd database
.\setup-database.ps1
```

</div>

הסקריפט מריץ את כל קובצי ה־SQL לפי סדר המספרים: יצירת בסיס הנתונים `DataControlsDemo`, הטבלאות העסקיות, המילון העסקי (metadata), נתוני הדוגמה, 10 בקרות לדוגמה ומשתמש ההרצה המוגבל.

- ברירת המחדל היא השרת `.\SQLEXPRESS`. לשרת אחר: `.\setup-database.ps1 -Server "localhost"`.
- הסקריפט **בונה את בסיס הנתונים מחדש** בכל הרצה, כך שאפשר להריץ אותו שוב כדי לחזור למצב ההתחלתי.
- אם PowerShell חוסם את ההרצה: `powershell -ExecutionPolicy Bypass -File .\setup-database.ps1`.

### 2. הגדרת מחרוזת החיבור (רק אם השרת אינו `.\SQLEXPRESS`)

מחרוזת החיבור נמצאת ב־[`backend/src/DataControls.Api/appsettings.json`](backend/src/DataControls.Api/appsettings.json), במפתח `Database:ConnectionString`. אפשר לעקוף אותה **בלי לשנות קבצים** באמצעות משתנה סביבה:

<div dir="ltr">

```powershell
$env:Database__ConnectionString = "Server=localhost;Database=DataControlsDemo;Trusted_Connection=True;TrustServerCertificate=True"
```

</div>

### 3. הרצת ה־API (פורט 5080)

<div dir="ltr">

```powershell
cd backend
dotnet run --project src/DataControls.Api --launch-profile http
```

</div>

תיעוד אינטראקטיבי של ה־API: http://localhost:5080/scalar

### 4. הרצת שירות ה־AI (פורט 8000)

בהרצה הראשונה יוצרים סביבה וירטואלית ומתקינים ספריות:

<div dir="ltr">

```powershell
cd ai-service
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
```

</div>

הרצה:

<div dir="ltr">

```powershell
.venv\Scripts\python -m uvicorn app.main:app --port 8000
```

</div>

תיעוד אינטראקטיבי: http://localhost:8000/docs. ברירת המחדל היא ספק ה־Mock, ואין שום קריאה לשירות חיצוני.

### 5. הרצת צד הלקוח (פורט 4200)

<div dir="ltr">

```powershell
cd frontend
npm install
npm start
```

</div>

הדפדפן נפתח בכתובת http://localhost:4200. בקשות ל־`/api` מועברות ל־API דרך `proxy.conf.json`.

### הרצה בפקודה אחת

אחרי ההתקנה הראשונה (שלבים 1, 4 ו־5), הסקריפט [`start-all.ps1`](start-all.ps1) פותח את שלושת התהליכים, כל אחד בחלון משלו:

<div dir="ltr">

```powershell
.\start-all.ps1
```

</div>

## בדיקות אוטומטיות

| חלק | פקודה | מספר בדיקות |
|---|---|---|
| .NET | `cd backend` ואז `dotnet test` | 66 |
| Python | `cd ai-service` ואז `.venv\Scripts\python -m pytest` | 32 |
| Angular | `cd frontend` ואז `npm test -- --watch=false` | 19 |

בדיקות ה־.NET וה־Python לא דורשות בסיס נתונים: הן בודקות את הלוגיקה מול מילון נתונים לדוגמה.

## מבנה התיקיות

<div dir="ltr">

```
DynamicDataControls/
├── database/              סקריפטי SQL ממוספרים + setup-database.ps1
├── backend/
│   ├── src/
│   │   ├── DataControls.Core/            לוגיקה עסקית: הגדרה, אימות, בניית שאילתה, תיאור בעברית
│   │   ├── DataControls.Infrastructure/  גישה ל־SQL Server, ולשירות ה־AI ב־HTTP
│   │   └── DataControls.Api/             Controllers, טיפול בשגיאות, הגדרות
│   └── tests/DataControls.Tests/         בדיקות xUnit
├── ai-service/
│   ├── app/               FastAPI: נקודות קצה, חוזה, ספקי AI (Mock / Claude)
│   └── tests/             בדיקות pytest
├── frontend/src/app/
│   ├── core/              מודלים, שירותי API, טיפול בשגיאות
│   ├── shared/            רכיבים משותפים
│   └── features/          מסכים: רשימת בקרות, עורך, תוצאות, יצירה בשפה חופשית
└── docs/                  מסמכי ההגשה
```

</div>

## סיור קצר במערכת

1. **הבקרות שלי:** רשימת 10 בקרות הדוגמה. כל בקרה מוצגת עם תיאור במילים ותוצאת ההרצה האחרונה. אפשר להריץ, לערוך, לשכפל ולמחוק.
2. **הרצה:** לחיצה על "הרצה" מציגה את מספר החריגות ואת הרשומות החריגות בטבלה. לכל רשומה מוצגת **הסיבה** שבגללה היא נכשלה. אפשר לסנן לפי סיבה, לחפש, למיין ולייצא ל־CSV.
3. **בקרה חדשה:** בוחרים נתונים עסקיים (למשל "עובדים"), ובונים תנאים מתוך שדות עם שמות עסקיים, כולל שדות של ישויות מקושרות, כמו "מחלקה ← מחלקה פעילה". אפשר לשלב קבוצות של "וגם"/"או". התיאור במילים מתעדכן תוך כדי, ואפשר לבצע **הרצת ניסיון** לפני שמירה.
4. **יצירה בשפה חופשית:** כותבים למשל "מצא עובדים פעילים שגילם נמוך מ־18 ומשכורתם גבוהה מ־15,000 ש"ח". המערכת מציגה את הבקרה המוצעת, איך כל חלק בטקסט הובן, ואזהרות. רק אחרי אישור המשתמש הבקרה נשמרת או רצה. באותו מסך מוצגות גם **הצעות יזומות** לבקרות איכות נתונים.

</div>
