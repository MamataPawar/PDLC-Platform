// ── Stage 3a: Code Generation Panel ──────────────────────────────────────────

import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { catchError, of } from 'rxjs';
import { PdlcApiService, CodeGenResult, CodeGenLanguage } from '../../core/services/pdlc-api.service';

@Component({
  selector: 'app-codegen-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTabsModule, MatSelectModule, MatFormFieldModule,
    MatInputModule, MatSnackBarModule],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a mat-icon-button [routerLink]="['/design', requirementId]"><mat-icon>arrow_back</mat-icon></a>
        <h1>Stage 3a — Code Generation</h1>
      </div>
      <div style="display:flex;gap:8px;align-items:center">
        <mat-form-field appearance="outline" style="width:180px;margin-bottom:-1.25em">
          <mat-label>Language</mat-label>
          <mat-select [(ngModel)]="selectedLanguage">
            <mat-option [value]="0">Both (.NET + Angular)</mat-option>
            <mat-option [value]="1">.NET Core only</mat-option>
            <mat-option [value]="2">Angular only</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-raised-button color="primary" (click)="generate()" [disabled]="generating()">
          <mat-spinner *ngIf="generating()" diameter="18" style="margin-right:8px;display:inline-block"/>
          {{ generating() ? 'Generating…' : '✦ Generate Code' }}
        </button>
        <a mat-stroked-button [routerLink]="['/testgen', requirementId]">Stage 4 → Tests</a>
      </div>
    </div>

    <div *ngIf="generating()" style="text-align:center;padding:48px">
      <mat-spinner diameter="48" style="margin:0 auto 16px"/>
      <p style="color:#6b7280">Claude is generating boilerplate code…</p>
    </div>

    <div *ngFor="let result of results()" class="card" style="margin-bottom:16px">
      <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px">
        <div>
          <span class="badge blue">{{result.language === 1 ? '.NET Core' : 'Angular'}}</span>
          <span style="margin-left:8px;font-family:monospace;font-size:.85rem">{{result.fileName}}</span>
        </div>
        <div style="display:flex;gap:8px">
          <button mat-icon-button (click)="copy(result.generatedContent)"><mat-icon>content_copy</mat-icon></button>
          <button mat-stroked-button (click)="downloadFile(result)"><mat-icon>download</mat-icon> Download</button>
        </div>
      </div>
      <pre class="code-block">{{result.generatedContent}}</pre>
      <div class="meta">{{result.tokensUsed | number}} tokens · {{result.modelVersion}} · {{result.createdAt | date:'short'}}</div>
    </div>

    <div *ngIf="!results().length && !generating()" class="card empty-state">
      No code generated yet. Click "Generate Code" to run Stage 3a.
    </div>
  `,
  styles: [`
    .page-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:24px; }
    .page-header h1 { margin:0; font-size:1.5rem; font-weight:600; }
    .card { background:white; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); padding:24px; }
    .code-block { background:#1e2536; color:#e2e8f0; border-radius:6px; padding:16px; font-size:.75rem;
      overflow:auto; max-height:600px; white-space:pre-wrap; word-break:break-all; }
    .meta { font-size:.75rem; color:#9ca3af; margin-top:8px; }
    .badge { display:inline-block; padding:2px 8px; border-radius:9999px; font-size:.72rem; font-weight:600; }
    .badge.blue { background:#dbeafe; color:#1e40af; }
    .empty-state { color:#9ca3af; text-align:center; padding:32px; }
    a { color:inherit; }
  `]
})
export class CodegenPanelComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(PdlcApiService);
  private snack = inject(MatSnackBar);

  requirementId = '';
  selectedLanguage = CodeGenLanguage.Both;
  generating = signal(false);
  results = signal<CodeGenResult[]>([]);

  ngOnInit() {
    this.requirementId = this.route.snapshot.paramMap.get('requirementId')!;
    this.api.getCodeGenResults(this.requirementId).pipe(catchError(() => of([])))
      .subscribe(r => this.results.set(r));
  }

  generate() {
    this.generating.set(true);
    this.api.generateCode(this.requirementId, this.selectedLanguage)
      .pipe(catchError(() => { this.generating.set(false); return of([]); }))
      .subscribe(r => { this.results.set(r); this.generating.set(false);
        this.snack.open('Code generated ✓', 'Close', { duration: 3000 }); });
  }

  copy(content: string) { navigator.clipboard.writeText(content); }

  downloadFile(result: CodeGenResult) {
    const blob = new Blob([result.generatedContent], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = result.fileName; a.click();
    URL.revokeObjectURL(url);
  }
}

// ── Stage 3b: PR Review Panel ─────────────────────────────────────────────────

@Component({
  selector: 'app-pr-review',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatProgressSpinnerModule, MatSnackBarModule, MatIconModule],
  template: `
    <div class="page-header"><h1>Stage 3b — PR Review</h1></div>
    <div class="card" style="margin-bottom:24px">
      <h2>Review a Pull Request</h2>
      <div style="display:grid;grid-template-columns:1fr 1fr 200px;gap:12px;margin-bottom:16px">
        <mat-form-field appearance="outline">
          <mat-label>ADO Project</mat-label>
          <input matInput [(ngModel)]="adoProject" placeholder="MyProject">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Repository</mat-label>
          <input matInput [(ngModel)]="repository" placeholder="my-repo">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>PR ID</mat-label>
          <input matInput type="number" [(ngModel)]="pullRequestId" placeholder="123">
        </mat-form-field>
      </div>
      <button mat-raised-button color="primary" (click)="review()" [disabled]="reviewing() || !adoProject || !repository || !pullRequestId">
        <mat-spinner *ngIf="reviewing()" diameter="18" style="margin-right:8px;display:inline-block"/>
        {{ reviewing() ? 'Reviewing…' : '✦ Run AI Review' }}
      </button>
    </div>

    <div *ngIf="result()" class="card">
      <h2>Review Result — PR #{{result()!.pullRequestId}}</h2>
      <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:16px">
        <div class="stat"><div class="stat-v">{{result()!.commentsPosted}}</div><div class="stat-l">Comments Posted</div></div>
        <div class="stat"><div class="stat-v">{{result()!.tokensUsed | number}}</div><div class="stat-l">Tokens Used</div></div>
        <div class="stat"><div class="stat-v" style="font-size:1rem">{{result()!.modelVersion}}</div><div class="stat-l">Model</div></div>
      </div>
      <p style="color:#6b7280;font-size:.85rem">Comments have been posted directly to the ADO pull request thread.</p>
    </div>
  `,
  styles: [`.page-header{display:flex;align-items:center;margin-bottom:24px} .page-header h1{margin:0;font-size:1.5rem;font-weight:600}
    .card{background:white;border-radius:8px;box-shadow:0 1px 3px rgba(0,0,0,.08);padding:24px}
    h2{margin:0 0 16px;font-size:1rem;font-weight:600}
    .stat{text-align:center;background:#f9fafb;border-radius:8px;padding:16px}
    .stat-v{font-size:1.5rem;font-weight:700} .stat-l{font-size:.75rem;color:#6b7280}`]
})
export class PrReviewComponent {
  private api = inject(PdlcApiService);
  private snack = inject(MatSnackBar);

  adoProject = '';
  repository = '';
  pullRequestId: number | null = null;
  reviewing = signal(false);
  result = signal<any>(null);

  review() {
    if (!this.adoProject || !this.repository || !this.pullRequestId) return;
    this.reviewing.set(true);
    this.api.reviewPullRequest(this.adoProject, this.repository, this.pullRequestId)
      .pipe(catchError(() => { this.snack.open('Review failed', 'Close', { duration: 4000 }); this.reviewing.set(false); return of(null); }))
      .subscribe(r => { this.result.set(r); this.reviewing.set(false);
        if (r) this.snack.open(`${r.commentsPosted} comments posted to PR ✓`, 'Close', { duration: 3000 }); });
  }
}

// ── Stage 4: Test Generation Panel ───────────────────────────────────────────

@Component({
  selector: 'app-testgen-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatProgressSpinnerModule, MatSnackBarModule, MatIconModule, MatTabsModule],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a mat-icon-button [routerLink]="['/codegen', requirementId]"><mat-icon>arrow_back</mat-icon></a>
        <h1>Stage 4 — Test Generation</h1>
      </div>
    </div>
    <div class="card" style="margin-bottom:24px">
      <h2>Paste source code to generate tests</h2>
      <mat-form-field appearance="outline" style="width:100%">
        <mat-label>Source file name</mat-label>
        <input matInput [(ngModel)]="sourceFileName" placeholder="RequirementAnalysisService.cs">
      </mat-form-field>
      <mat-form-field appearance="outline" style="width:100%;margin-top:8px">
        <mat-label>Source content</mat-label>
        <textarea matInput [(ngModel)]="sourceContent" rows="10" placeholder="Paste C# or TypeScript source here…"></textarea>
      </mat-form-field>
      <button mat-raised-button color="primary" (click)="generate()" [disabled]="generating() || !sourceContent || !sourceFileName" style="margin-top:12px">
        <mat-spinner *ngIf="generating()" diameter="18" style="margin-right:8px;display:inline-block"/>
        {{ generating() ? 'Generating…' : '✦ Generate Tests' }}
      </button>
    </div>

    <div *ngFor="let result of results()" class="card" style="margin-bottom:16px">
      <div style="display:flex;justify-content:space-between;margin-bottom:12px">
        <div>
          <span class="badge">{{result.target === 1 ? 'xUnit (.NET)' : 'Jasmine (Angular)'}}</span>
          <span style="margin-left:8px;font-family:monospace;font-size:.85rem">{{result.outputFileName}}</span>
        </div>
        <button mat-icon-button (click)="copy(result.generatedContent)"><mat-icon>content_copy</mat-icon></button>
      </div>
      <pre class="code-block">{{result.generatedContent}}</pre>
    </div>
  `,
  styles: [`.page-header{display:flex;align-items:center;justify-content:space-between;margin-bottom:24px}
    .page-header h1{margin:0;font-size:1.5rem;font-weight:600}
    .card{background:white;border-radius:8px;box-shadow:0 1px 3px rgba(0,0,0,.08);padding:24px}
    h2{margin:0 0 16px;font-size:1rem;font-weight:600}
    .code-block{background:#1e2536;color:#e2e8f0;border-radius:6px;padding:16px;font-size:.75rem;overflow:auto;max-height:500px;white-space:pre-wrap;word-break:break-all}
    .badge{display:inline-block;padding:2px 8px;border-radius:9999px;font-size:.72rem;font-weight:600;background:#dbeafe;color:#1e40af}
    a{color:inherit}`]
})
export class TestgenPanelComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(PdlcApiService);
  private snack = inject(MatSnackBar);

  requirementId = '';
  sourceContent = '';
  sourceFileName = '';
  generating = signal(false);
  results = signal<any[]>([]);

  ngOnInit() { this.requirementId = this.route.snapshot.paramMap.get('requirementId')!; }

  generate() {
    this.generating.set(true);
    this.api.generateTests(this.sourceContent, this.sourceFileName, 3, this.requirementId || undefined)
      .pipe(catchError(() => { this.generating.set(false); return of([]); }))
      .subscribe(r => { this.results.set(r); this.generating.set(false);
        this.snack.open('Tests generated ✓', 'Close', { duration: 3000 }); });
  }

  copy(content: string) { navigator.clipboard.writeText(content); }
}
