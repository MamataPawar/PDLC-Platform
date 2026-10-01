import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { catchError, of } from 'rxjs';
import { PdlcApiService, DesignArtifact, DesignArtifactType } from '../../core/services/pdlc-api.service';

@Component({
  selector: 'app-design-panel',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatCardModule,
    MatTabsModule, MatProgressSpinnerModule, MatIconModule, MatSnackBarModule],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a mat-icon-button [routerLink]="['/requirements', requirementId]"><mat-icon>arrow_back</mat-icon></a>
        <h1>Stage 2 — Design Artifacts</h1>
      </div>
      <div style="display:flex;gap:8px">
        <button mat-raised-button color="primary" (click)="generate()" [disabled]="generating()">
          <mat-spinner *ngIf="generating()" diameter="18" style="margin-right:8px;display:inline-block"/>
          {{ artifacts().length ? '↻ Regenerate All' : '✦ Generate Design' }}
        </button>
        <a mat-stroked-button [routerLink]="['/codegen', requirementId]">Stage 3 → Code</a>
      </div>
    </div>

    <div *ngIf="generating()" style="text-align:center;padding:48px">
      <mat-spinner diameter="48" style="margin:0 auto 16px"/>
      <p style="color:#6b7280">Claude is generating design artifacts…</p>
    </div>

    <mat-tab-group *ngIf="artifacts().length && !generating()">
      <!-- Component Tree tab -->
      <mat-tab label="🌳 Component Tree">
        <div class="card tab-content" *ngIf="getArtifact(1) as artifact">
          <div class="artifact-header">
            <span>{{artifact.title}}</span>
            <button mat-icon-button (click)="copy(artifact.content)" title="Copy"><mat-icon>content_copy</mat-icon></button>
            <button mat-stroked-button (click)="regenerate(1)">↻ Regenerate</button>
          </div>
          <pre class="code-block">{{formatJson(artifact.content)}}</pre>
          <div class="meta">{{artifact.tokensUsed | number}} tokens · {{artifact.modelVersion}} · {{artifact.createdAt | date:'short'}}</div>
        </div>
        <div *ngIf="!getArtifact(1)" class="empty-state">Not generated yet</div>
      </mat-tab>

      <!-- API Contract tab -->
      <mat-tab label="📄 API Contract">
        <div class="card tab-content" *ngIf="getArtifact(2) as artifact">
          <div class="artifact-header">
            <span>{{artifact.title}}</span>
            <button mat-icon-button (click)="copy(artifact.content)" title="Copy"><mat-icon>content_copy</mat-icon></button>
            <button mat-stroked-button (click)="regenerate(2)">↻ Regenerate</button>
          </div>
          <pre class="code-block yaml">{{artifact.content}}</pre>
          <div class="meta">Format: YAML/OpenAPI 3.0 · {{artifact.modelVersion}} · {{artifact.createdAt | date:'short'}}</div>
        </div>
        <div *ngIf="!getArtifact(2)" class="empty-state">Not generated yet</div>
      </mat-tab>

      <!-- Data Model tab -->
      <mat-tab label="🗃 Data Model">
        <div class="card tab-content" *ngIf="getArtifact(3) as artifact">
          <div class="artifact-header">
            <span>{{artifact.title}}</span>
            <button mat-icon-button (click)="copy(artifact.content)" title="Copy"><mat-icon>content_copy</mat-icon></button>
            <button mat-stroked-button (click)="regenerate(3)">↻ Regenerate</button>
          </div>
          <pre class="code-block csharp">{{artifact.content}}</pre>
          <div class="meta">Format: C# / Markdown · {{artifact.modelVersion}} · {{artifact.createdAt | date:'short'}}</div>
        </div>
        <div *ngIf="!getArtifact(3)" class="empty-state">Not generated yet</div>
      </mat-tab>
    </mat-tab-group>

    <div *ngIf="!artifacts().length && !generating()" class="empty-state card">
      No design artifacts yet. Click "Generate Design" to run Stage 2.
    </div>
  `,
  styles: [`
    .page-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:24px; }
    .page-header h1 { margin:0; font-size:1.5rem; font-weight:600; }
    .card { background:white; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); padding:24px; }
    .tab-content { margin-top:16px; }
    .artifact-header { display:flex; align-items:center; gap:8px; margin-bottom:12px; font-weight:600; }
    .code-block { background:#1e2536; color:#e2e8f0; border-radius:6px; padding:16px; font-size:.8rem;
      overflow-x:auto; max-height:500px; overflow-y:auto; white-space:pre-wrap; word-break:break-all; }
    .meta { font-size:.75rem; color:#9ca3af; margin-top:8px; }
    .empty-state { color:#9ca3af; text-align:center; padding:32px; }
    a { color:inherit; }
  `]
})
export class DesignPanelComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(PdlcApiService);
  private snack = inject(MatSnackBar);

  requirementId = '';
  generating = signal(false);
  artifacts = signal<DesignArtifact[]>([]);

  ngOnInit() {
    this.requirementId = this.route.snapshot.paramMap.get('requirementId')!;
    this.api.getDesignArtifacts(this.requirementId).pipe(catchError(() => of([])))
      .subscribe(a => this.artifacts.set(a));
  }

  generate() {
    this.generating.set(true);
    this.api.generateDesign(this.requirementId).pipe(catchError(err => {
      this.snack.open('Design generation failed', 'Close', { duration: 4000 });
      this.generating.set(false);
      return of([]);
    })).subscribe(a => {
      this.artifacts.set(a);
      this.generating.set(false);
      this.snack.open('Design artifacts generated ✓', 'Close', { duration: 3000 });
    });
  }

  regenerate(type: DesignArtifactType) {
    this.api.regenerateArtifact(this.requirementId, type)
      .pipe(catchError(() => of(null)))
      .subscribe(a => {
        if (!a) return;
        this.artifacts.update(list => [...list.filter(x => x.type !== type), a]);
        this.snack.open('Artifact regenerated ✓', 'Close', { duration: 2000 });
      });
  }

  getArtifact(type: DesignArtifactType): DesignArtifact | undefined {
    return this.artifacts().find(a => a.type === type);
  }

  formatJson(content: string): string {
    try { return JSON.stringify(JSON.parse(content), null, 2); }
    catch { return content; }
  }

  copy(content: string) {
    navigator.clipboard.writeText(content);
    this.snack.open('Copied to clipboard', 'Close', { duration: 1500 });
  }
}
