import { Routes } from '@angular/router';

// Lazy loading: each screen's code is downloaded only when the user reaches it - a faster first load.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'controls' },
  {
    path: 'controls',
    title: 'הבקרות שלי',
    loadComponent: () => import('./features/controls/control-list/control-list').then((m) => m.ControlList),
  },
  {
    path: 'controls/new',
    title: 'בקרה חדשה',
    loadComponent: () => import('./features/controls/control-editor/control-editor').then((m) => m.ControlEditor),
  },
  {
    path: 'controls/:id/edit',
    title: 'עריכת בקרה',
    loadComponent: () => import('./features/controls/control-editor/control-editor').then((m) => m.ControlEditor),
  },
  {
    path: 'controls/:id/results',
    title: 'תוצאות בקרה',
    loadComponent: () => import('./features/results/control-results/control-results').then((m) => m.ControlResults),
  },
  {
    path: 'ai',
    title: 'יצירה בשפה חופשית',
    loadComponent: () => import('./features/ai-assistant/ai-assistant').then((m) => m.AiAssistant),
  },
  { path: '**', redirectTo: 'controls' },
];
