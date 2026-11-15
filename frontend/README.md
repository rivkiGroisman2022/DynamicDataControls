<div dir="rtl">

# צד הלקוח: data-controls-ui

אפליקציית Angular 21 (Standalone, ‏Signals, ‏Angular Material) בעברית ובכיווניות RTL.

<div dir="ltr">

```powershell
npm install
npm start                     # http://localhost:4200, עם proxy ל־API בפורט 5080
npm test -- --watch=false     # בדיקות Vitest
npm run build                 # בנייה לייצור, לתיקייה dist/
```

</div>

| תיקייה | תוכן |
|---|---|
| `src/app/core` | מודלים (חוזה ההגדרה), שירותי API, Cache של המילון העסקי, טיפול בשגיאות |
| `src/app/shared` | רכיבים ופונקציות משותפים: תג חומרה, חלון אישור, עיצוב ערכים |
| `src/app/features/controls` | רשימת הבקרות, עורך הבקרה ובונה התנאים (רכיב רקורסיבי) |
| `src/app/features/results` | טבלת התוצאות לשימוש חוזר, ומסך הרצת בקרה |
| `src/app/features/ai-assistant` | יצירת בקרה בשפה חופשית, והצעות יזומות |

הוראות ההרצה המלאות של המערכת נמצאות ב־[README הראשי](../README.md).

</div>
