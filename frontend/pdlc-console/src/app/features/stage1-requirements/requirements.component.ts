import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { catchError, of } from 'rxjs';
import { PdlcApiService, RequirementDocument, RequirementStatus } from '../../core/services/pdlc-api.service';

@Component({
  selector: 'app-requirements',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, RouterLink,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatCardModule,
    MatProgressSpinnerModule, MatCheckboxModule, MatChipsModule, MatIconModule, MatSnackBarModule,
  ],
  template: `
    <div class="page-header">
      <h1>Stage 1 — Requirement Analysis</h1>
    </div>

    <!-- Analyse form -->
    <div class="card" style="margin-bottom:24px">
      <h2>Analyse New Requirement</h2>
      <form [formGroup]="form" (ngSubmit)="analyse()">
        <mat-form-field appearance="outline" style="width:100%">
          <mat-label>Raw requirement / user story (free text)</mat-label>
          <textarea matInput formControlName="rawInput" rows="6"
            placeholder="As a store manager, I want to see real-time inventory levels per store so that I can identify phantom stock before placing new orders...">
          </textarea>
          <mat-error *ngIf="form.get('rawInput')?.hasError('required')">Required</mat-error>
          <mat-error *ngIf="form.get('rawInput')?.hasError('minlength')">Minimum 30 characters</mat-error>
        </mat-form-field>

        <div style="display:flex;gap:16px;align-items:center;margin-top:8px;flex-wrap:wrap">
          <mat-form-field appearance="outline" style="width:200px">
            <mat-label>ADO Project (optional)</mat-label>
            <input matInput formControlName="adoProject" placeholder="MyProject">
          </mat-form-field>
          <mat-checkbox formControlName="syncToAdo" color="primary">
            Auto-sync to ADO Boards
          </mat-checkbox>
          <div style="flex:1"></div>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="form.invalid || isAnalysing()">
            <mat-spinner *ngIf="isAnalysing()" diameter="18" style="margin-right:8px;display:inline-block"/>
            {{ isAnalysing() ? 'Analysing…' : '✦ Analyse with Claude' }}
          </button>
        </div>
      </form>

      <!-- Error -->
      <div *ngIf="error()" class="error-banner">
        <mat-icon>error_outline</mat-icon> {{error()}}
      </div>
    </div>

    <!-- Latest result -->
    <div *ngIf="latestResult()" class="card result-card" style="margin-bottom:24px">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:16px">
        <div>
          <h2 style="margin:0">{{latestResult()!.title}}</h2>
          <p style="color:#6b7280;margin:4px 0 0">{{latestResult()!.summary}}</p>
        </div>
        <div style="display:flex;gap:8px;align-items:center">
          <span class="badge" [class]="statusClass(latestResult()!.status)">
            {{statusLabel(latestResult()!.status)}}
          </span>
          <a mat-stroked-button [routerLink]="['/requirements', latestResult()!.id]">
            <mat-icon>open_in_new</mat-icon> Full detail
          </a>
          <a mat-raised-button color="accent" [routerLink]="['/design', latestResult()!.id]">
            Stage 2 → Design
          </a>
        </div>
      </div>

      <div class="artifacts-grid">
        <!-- Acceptance Criteria -->
        <div class="artifact-box">
          <div class="artifact-title"><mat-icon>check_circle</mat-icon> Acceptance Criteria ({{latestResult()!.acceptanceCriteria.length}})</div>
          <ol>
            <li *ngFor="let ac of latestResult()!.acceptanceCriteria">{{ac.description}}</li>
          </ol>
        </div>

        <!-- Effort Estimate -->
        <div class="artifact-box" *ngIf="latestResult()!.effortEstimate">
          <div class="artifact-title"><mat-icon>schedule</mat-icon> Effort Estimate</div>
          <div class="estimate-row"><strong>{{latestResult()!.effortEstimate!.storyPoints}} SP</strong> — {{latestResult()!.effortEstimate!.confidence}} confidence</div>
          <div class="estimate-row">{{latestResult()!.effortEstimate!.estimatedDaysLow}}–{{latestResult()!.effortEstimate!.estimatedDaysHigh}} days</div>
          <p style="font-size:.85rem;color:#6b7280;margin-top:8px">{{latestResult()!.effortEstimate!.rationale}}</p>
        </div>

        <!-- Risk Flags -->
        <div class="artifact-box" *ngIf="latestResult()!.riskFlags.length">
          <div class="artifact-title"><mat-icon>warning</mat-icon> Risk Flags ({{latestResult()!.riskFlags.length}})</div>
          <div *ngFor="let r of latestResult()!.riskFlags" class="risk-item">
            <span class="badge" [class]="r.severity">{{r.severity}}</span>
            {{r.description}}
          </div>
        </div>

        <!-- Edge Cases -->
        <div class="artifact-box" *ngIf="latestResult()!.edgeCases.length">
          <div class="artifact-title"><mat-icon>bug_report</mat-icon> Edge Cases ({{latestResult()!.edgeCases.length}})</div>
          <div *ngFor="let ec of latestResult()!.edgeCases" class="risk-item">
            <span class="badge" [class]="ec.severity">{{ec.severity}}</span>
            {{ec.description}}
          </div>
        </div>
      </div>

      <div style="font-size:.75rem;color:#9ca3af;margin-top:16px">
        Model: {{latestResult()!.modelVersion}} · {{latestResult()!.tokensUsed | number}} tokens
        <span *ngIf="latestResult()!.adoWorkItemId"> · ADO #{{latestResult()!.adoWorkItemId}}</span>
      </div>
    </div>

    <!-- Requirements list -->
    <div class="card">
      <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:16px">
        <h2 style="margin:0">All Requirements</h2>
        <button mat-icon-button (click)="loadList()" title="Refresh"><mat-icon>refresh</mat-icon></button>
      </div>
      <div *ngIf="listLoading()" style="text-align:center;padding:24px"><mat-spinner diameter="32"/></div>
      <div *ngIf="!listLoading()">
        <div *ngFor="let req of requirements()" class="req-row">
          <div style="flex:1">
            <a [routerLink]="['/requirements', req.id]" class="req-title">{{req.title}}</a>
            <div style="font-size:.8rem;color:#6b7280">{{req.summary | slice:0:100}}…</div>
          </div>
          <div style="display:flex;align-items:center;gap:8px">
            <span class="badge" [class]="statusClass(req.status)">{{statusLabel(req.status)}}</span>
            <span style="font-size:.75rem;color:#9ca3af">{{req.effortEstimate?.storyPoints ?? '?'}} SP</span>
          </div>
        </div>
        <div *ngIf="requirements().length === 0" style="color:#9ca3af;padding:16px 0;text-align:center">
          No requirements yet. Analyse your first one above.
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:24px; }
    .page-header h1 { margin:0; font-size:1.5rem; font-weight:600; }
    .card { background:white; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); padding:24px; }
    h2 { margin:0 0 16px; font-size:1rem; font-weight:600; }
    .error-banner { background:#fff5f5; border:1px solid #fed7d7; border-radius:6px; padding:12px 16px;
      margin-top:12px; color:#c53030; display:flex; align-items:center; gap:8px; }
    .artifacts-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(280px,1fr)); gap:16px; }
    .artifact-box { background:#f9fafb; border-radius:6px; padding:16px; }
    .artifact-title { font-weight:600; font-size:.85rem; display:flex; align-items:center; gap:6px; margin-bottom:10px; }
    .estimate-row { font-size:.9rem; margin-bottom:4px; }
    .risk-item { display:flex; align-items:flex-start; gap:6px; margin-bottom:6px; font-size:.85rem; }
    .req-row { display:flex; align-items:center; gap:12px; padding:10px 0;
      border-bottom:1px solid #f3f4f6; &:last-child { border:none; } }
    .req-title { font-weight:500; color:#4f46e5; text-decoration:none;
      &:hover { text-decoration:underline; } }
    .badge { display:inline-block; padding:2px 8px; border-radius:9999px;
      font-size:.72rem; font-weight:600; text-transform:capitalize; }
    .badge.draft, .badge.low   { background:#f3f4f6; color:#374151; }
    .badge.analyzed, .badge.medium { background:#fef3c7; color:#92400e; }
    .badge.approved, .badge.high { background:#fee2e2; color:#991b1b; }
    .badge.indesign, .badge.indev { background:#dbeafe; color:#1e40af; }
    .badge.done    { background:#d1fae5; color:#065f46; }
    .result-card { border-left:4px solid #4f46e5; }
  `]
})
export class RequirementsComponent implements OnInit {
  private api = inject(PdlcApiService);
  private fb = inject(FormBuilder);
  private snack = inject(MatSnackBar);

  form = this.fb.group({
    rawInput: ['', [Validators.required, Validators.minLength(30)]],
    adoProject: [''],
    syncToAdo: [false],
  });

  isAnalysing = signal(false);
  error = signal<string | null>(null);
  latestResult = signal<RequirementDocument | null>(null);
  requirements = signal<RequirementDocument[]>([]);
  listLoading = signal(true);

  ngOnInit() { this.loadList(); }

  analyse() {
    if (this.form.invalid) return;
    this.isAnalysing.set(true);
    this.error.set(null);

    const { rawInput, adoProject, syncToAdo } = this.form.value;
    this.api.analyzeRequirement(rawInput!, adoProject || undefined, syncToAdo ?? false)
      .pipe(catchError(err => {
        this.error.set(err.error?.detail ?? 'Analysis failed — check the API is running on :5100');
        this.isAnalysing.set(false);
        return of(null);
      }))
      .subscribe(result => {
        if (!result) return;
        this.latestResult.set(result);
        this.isAnalysing.set(false);
        this.snack.open('Requirement analysed ✓', 'Close', { duration: 3000 });
        this.loadList();
      });
  }

  loadList() {
    this.listLoading.set(true);
    this.api.listRequirements().pipe(catchError(() => of([])))
      .subscribe(docs => { this.requirements.set(docs); this.listLoading.set(false); });
  }

  statusLabel(s: RequirementStatus): string {
    return RequirementStatus[s] ?? 'unknown';
  }

  statusClass(s: RequirementStatus): string {
    return RequirementStatus[s]?.toLowerCase() ?? 'draft';
  }
}
