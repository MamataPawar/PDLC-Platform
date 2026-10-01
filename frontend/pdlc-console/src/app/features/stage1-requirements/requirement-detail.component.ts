import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { catchError, of } from 'rxjs';
import {
  PdlcApiService,
  RequirementDocument,
  RequirementStatus,
  ConversationTurn,
} from '../../core/services/pdlc-api.service';

@Component({
  selector: 'app-requirement-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    MatTabsModule, MatFormFieldModule, MatInputModule, MatSnackBarModule,
  ],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a mat-icon-button routerLink="/requirements"><mat-icon>arrow_back</mat-icon></a>
        <h1>{{doc()?.title ?? 'Loading…'}}</h1>
      </div>
      <div style="display:flex;gap:8px" *ngIf="doc()">
        <span class="badge" [class]="statusClass(doc()!.status)">{{statusLabel(doc()!.status)}}</span>
        <a mat-raised-button color="primary" [routerLink]="['/design', id]">Stage 2 → Design</a>
      </div>
    </div>

    <div *ngIf="loading()" style="text-align:center;padding:48px"><mat-spinner/></div>

    <div *ngIf="doc() && !loading()">
      <!-- Summary banner -->
      <div class="card summary-card">
        <p style="margin:0;line-height:1.6">{{doc()!.summary}}</p>
        <div style="font-size:.75rem;color:#9ca3af;margin-top:12px">
          {{doc()!.tokensUsed | number}} tokens · {{doc()!.modelVersion}}
          <span *ngIf="doc()!.adoWorkItemId">
            · <a [href]="doc()!.adoWorkItemUrl" target="_blank">ADO #{{doc()!.adoWorkItemId}}</a>
          </span>
          <span *ngIf="!doc()!.adoWorkItemId && adoProject">
            · <button mat-button color="accent" (click)="syncAdo()" style="font-size:.75rem">Sync to ADO Boards</button>
          </span>
        </div>
      </div>

      <mat-tab-group style="margin-top:16px">

        <!-- Acceptance Criteria -->
        <mat-tab label="✓ Acceptance Criteria ({{doc()!.acceptanceCriteria.length}})">
          <div class="card tab-content">
            <ol class="criteria-list">
              <li *ngFor="let ac of doc()!.acceptanceCriteria" [class.testable]="ac.isTestable">
                {{ac.description}}
                <span *ngIf="ac.isTestable" class="testable-tag">testable</span>
              </li>
            </ol>
          </div>
        </mat-tab>

        <!-- Edge Cases -->
        <mat-tab label="⚡ Edge Cases ({{doc()!.edgeCases.length}})">
          <div class="card tab-content">
            <div *ngFor="let ec of doc()!.edgeCases" class="item-row">
              <span class="badge sev-{{ec.severity}}">{{ec.severity}}</span>
              <div style="flex:1">
                <div style="font-weight:500">{{ec.description}}</div>
                <div *ngIf="ec.mitigationNote" style="font-size:.8rem;color:#6b7280;margin-top:2px">
                  Mitigation: {{ec.mitigationNote}}
                </div>
              </div>
            </div>
          </div>
        </mat-tab>

        <!-- Risk Flags -->
        <mat-tab label="⚠ Risks ({{doc()!.riskFlags.length}})">
          <div class="card tab-content">
            <div *ngFor="let r of doc()!.riskFlags" class="item-row">
              <span class="badge sev-{{r.severity}}">{{r.severity}}</span>
              <div style="flex:1">
                <div style="font-weight:500">{{r.description}}</div>
                <div *ngIf="r.mitigation" style="font-size:.8rem;color:#6b7280;margin-top:2px">
                  {{r.mitigation}}
                </div>
              </div>
            </div>
          </div>
        </mat-tab>

        <!-- Dependencies -->
        <mat-tab label="🔗 Dependencies ({{doc()!.dependencies.length}})">
          <div class="card tab-content">
            <div *ngFor="let d of doc()!.dependencies" class="item-row">
              <span class="badge dep-type">{{d.type}}</span>
              <div style="flex:1">
                <div style="font-weight:500">{{d.name}}</div>
                <div *ngIf="d.notes" style="font-size:.8rem;color:#6b7280">{{d.notes}}</div>
              </div>
            </div>
          </div>
        </mat-tab>

        <!-- Effort Estimate -->
        <mat-tab label="📏 Estimate" *ngIf="doc()!.effortEstimate">
          <div class="card tab-content">
            <div class="estimate-grid">
              <div class="est-stat">
                <div class="est-val">{{doc()!.effortEstimate!.storyPoints}}</div>
                <div class="est-lbl">Story Points</div>
              </div>
              <div class="est-stat">
                <div class="est-val">{{doc()!.effortEstimate!.estimatedDaysLow}}–{{doc()!.effortEstimate!.estimatedDaysHigh}}</div>
                <div class="est-lbl">Days Range</div>
              </div>
              <div class="est-stat">
                <div class="est-val" style="font-size:1rem;text-transform:capitalize">{{doc()!.effortEstimate!.confidence}}</div>
                <div class="est-lbl">Confidence</div>
              </div>
            </div>
            <p style="font-size:.9rem;color:#374151;margin-top:16px">{{doc()!.effortEstimate!.rationale}}</p>
          </div>
        </mat-tab>

        <!-- Conversation (Multi-turn Refinement) -->
        <mat-tab label="💬 Conversation ({{doc()!.conversationHistory.length}} turns)">
          <div class="card tab-content">
            <!-- Chat history -->
            <div class="chat-container">
              <div *ngFor="let turn of conversation()" class="chat-bubble" [class.user]="turn.role === 'user'" [class.ai]="turn.role === 'assistant'">
                <div class="turn-role">{{turn.role === 'user' ? 'You' : 'Claude'}}</div>
                <div class="turn-content">{{turn.content}}</div>
                <div *ngIf="turn.tokensUsed" class="turn-meta">{{turn.tokensUsed}} tokens</div>
              </div>
            </div>

            <!-- Refine input -->
            <div style="display:flex;gap:8px;margin-top:16px">
              <mat-form-field appearance="outline" style="flex:1">
                <mat-label>Ask Claude to refine this requirement…</mat-label>
                <input matInput [(ngModel)]="refineMessage"
                  (keyup.enter)="refine()"
                  placeholder="e.g. Add acceptance criteria for the offline scenario">
              </mat-form-field>
              <button mat-raised-button color="primary" (click)="refine()"
                      [disabled]="refining() || !refineMessage.trim()">
                <mat-spinner *ngIf="refining()" diameter="18" style="margin-right:6px;display:inline-block"/>
                Send
              </button>
            </div>
          </div>
        </mat-tab>

      </mat-tab-group>
    </div>
  `,
  styles: [`
    .page-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:24px; }
    .page-header h1 { margin:0; font-size:1.4rem; font-weight:600; }
    .card { background:white; border-radius:8px; box-shadow:0 1px 3px rgba(0,0,0,.08); padding:24px; }
    .summary-card { border-left:4px solid #4f46e5; margin-bottom:0; }
    .tab-content { margin-top:16px; }
    .criteria-list { padding-left:20px; line-height:1.8; }
    .criteria-list li.testable { color:#065f46; }
    .testable-tag { background:#d1fae5; color:#065f46; border-radius:4px; padding:1px 6px; font-size:.7rem; margin-left:6px; }
    .item-row { display:flex; gap:10px; align-items:flex-start; padding:10px 0; border-bottom:1px solid #f3f4f6; }
    .item-row:last-child { border:none; }
    .estimate-grid { display:grid; grid-template-columns:repeat(3,1fr); gap:16px; }
    .est-stat { background:#f9fafb; border-radius:8px; padding:16px; text-align:center; }
    .est-val { font-size:1.8rem; font-weight:700; color:#4f46e5; }
    .est-lbl { font-size:.75rem; color:#6b7280; margin-top:4px; }
    .chat-container { display:flex; flex-direction:column; gap:12px; max-height:400px; overflow-y:auto; }
    .chat-bubble { max-width:85%; border-radius:10px; padding:12px 16px; }
    .chat-bubble.user { background:#eff6ff; align-self:flex-end; border-bottom-right-radius:2px; }
    .chat-bubble.ai { background:#f9fafb; align-self:flex-start; border-bottom-left-radius:2px; border:1px solid #e5e7eb; }
    .turn-role { font-size:.7rem; font-weight:600; color:#6b7280; text-transform:uppercase; margin-bottom:4px; }
    .turn-content { font-size:.9rem; line-height:1.5; white-space:pre-wrap; }
    .turn-meta { font-size:.7rem; color:#9ca3af; margin-top:4px; }
    .badge { display:inline-block; padding:2px 8px; border-radius:9999px; font-size:.72rem; font-weight:600; }
    .badge.sev-high { background:#fee2e2; color:#991b1b; }
    .badge.sev-medium { background:#fef3c7; color:#92400e; }
    .badge.sev-low { background:#f3f4f6; color:#374151; }
    .badge.dep-type { background:#dbeafe; color:#1e40af; }
    .badge.draft { background:#f3f4f6; color:#374151; }
    .badge.analyzed { background:#fef3c7; color:#92400e; }
    .badge.approved { background:#d1fae5; color:#065f46; }
    .badge.indesign,.badge.indev { background:#dbeafe; color:#1e40af; }
    .badge.done { background:#d1fae5; color:#065f46; }
    a { color:inherit; text-decoration:none; }
  `]
})
export class RequirementDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(PdlcApiService);
  private snack = inject(MatSnackBar);

  id = '';
  adoProject = '';
  doc = signal<RequirementDocument | null>(null);
  conversation = signal<ConversationTurn[]>([]);
  loading = signal(true);
  refining = signal(false);
  refineMessage = '';

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id')!;
    this.api.getRequirement(this.id).pipe(catchError(() => of(null)))
      .subscribe(d => {
        this.doc.set(d);
        this.loading.set(false);
        if (d) this.conversation.set(d.conversationHistory ?? []);
      });
  }

  refine() {
    if (!this.refineMessage.trim()) return;
    this.refining.set(true);
    const msg = this.refineMessage;
    this.refineMessage = '';

    this.api.refineRequirement(this.id, msg)
      .pipe(catchError(() => { this.refining.set(false); return of(null); }))
      .subscribe(turn => {
        if (!turn) return;
        this.conversation.update(c => [
          ...c,
          { id: crypto.randomUUID(), role: 'user', content: msg, turnIndex: c.length },
          turn,
        ]);
        this.refining.set(false);
        this.snack.open('Requirement refined ✓', 'Close', { duration: 2000 });
      });
  }

  syncAdo() {
    if (!this.adoProject) { this.snack.open('Enter an ADO Project name first', 'Close', { duration: 3000 }); return; }
    this.api.syncRequirementToAdo(this.id, this.adoProject)
      .pipe(catchError(() => of(null)))
      .subscribe(() => this.snack.open('Synced to ADO Boards ✓', 'Close', { duration: 3000 }));
  }

  statusLabel(s: RequirementStatus): string { return RequirementStatus[s] ?? 'unknown'; }
  statusClass(s: RequirementStatus): string { return RequirementStatus[s]?.toLowerCase() ?? 'draft'; }
}
