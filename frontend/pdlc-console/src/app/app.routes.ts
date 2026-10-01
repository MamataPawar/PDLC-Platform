import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'requirements', pathMatch: 'full' },
  {
    path: 'requirements',
    loadComponent: () =>
      import('./features/stage1-requirements/requirements.component').then(m => m.RequirementsComponent),
    title: 'Stage 1 — Requirements',
  },
  {
    path: 'requirements/:id',
    loadComponent: () =>
      import('./features/stage1-requirements/requirement-detail.component').then(m => m.RequirementDetailComponent),
    title: 'Requirement Detail',
  },
  {
    path: 'design/:requirementId',
    loadComponent: () =>
      import('./features/stage2-design/design-panel.component').then(m => m.DesignPanelComponent),
    title: 'Stage 2 — Design',
  },
  {
    path: 'codegen/:requirementId',
    loadComponent: () =>
      import('./features/stage3-codegen/codegen-panel.component').then(m => m.CodegenPanelComponent),
    title: 'Stage 3a — Code Generation',
  },
  {
    path: 'pr-review',
    loadComponent: () =>
      import('./features/stage3b-review/pr-review.component').then(m => m.PrReviewComponent),
    title: 'Stage 3b — PR Review',
  },
  {
    path: 'testgen/:requirementId',
    loadComponent: () =>
      import('./features/stage4-testgen/testgen-panel.component').then(m => m.TestgenPanelComponent),
    title: 'Stage 4 — Test Generation',
  },
  {
    path: 'usage',
    loadComponent: () =>
      import('./features/stage1-requirements/usage-dashboard.component').then(m => m.UsageDashboardComponent),
    title: 'Usage & Tokens',
  },
  { path: '**', redirectTo: 'requirements' },
];
