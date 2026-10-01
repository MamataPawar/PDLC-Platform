import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';

interface NavItem {
  icon: string;
  label: string;
  route: string;
  stage: string;
  color: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatSidenavModule, MatToolbarModule, MatIconModule,
    MatListModule, MatButtonModule, MatTooltipModule, MatDividerModule,
  ],
  template: `
    <mat-sidenav-container class="shell">
      <!-- ── Side Nav ─────────────────────────────────── -->
      <mat-sidenav mode="side" opened class="sidenav">
        <div class="brand">
          <mat-icon class="brand-icon">auto_awesome</mat-icon>
          <div>
            <div class="brand-title">PDLC Console</div>
            <div class="brand-sub">AI-Native Dev Lifecycle</div>
          </div>
        </div>

        <mat-nav-list>
          <div class="nav-section-label">PDLC Stages</div>
          <a mat-list-item
             *ngFor="let item of navItems"
             [routerLink]="item.route"
             routerLinkActive="active-link"
             [matTooltip]="item.label"
             matTooltipPosition="right">
            <div class="nav-item-inner">
              <div class="stage-chip" [style.background]="item.color + '22'" [style.color]="item.color">
                {{item.stage}}
              </div>
              <span class="nav-label">{{item.label}}</span>
            </div>
          </a>
        </mat-nav-list>

        <mat-divider style="margin:8px 0"/>

        <mat-nav-list>
          <div class="nav-section-label">Observability</div>
          <a mat-list-item routerLink="/usage" routerLinkActive="active-link">
            <div class="nav-item-inner">
              <mat-icon style="font-size:18px;color:#6b7280">analytics</mat-icon>
              <span class="nav-label">Token Usage</span>
            </div>
          </a>
        </mat-nav-list>

        <div class="sidenav-footer">
          <div class="model-chip">claude-sonnet-4-6</div>
        </div>
      </mat-sidenav>

      <!-- ── Main content ─────────────────────────────── -->
      <mat-sidenav-content class="main-content">
        <mat-toolbar class="topbar">
          <span class="topbar-title">PDLC AI Platform</span>
          <span style="flex:1"></span>
          <a mat-icon-button href="https://docs.anthropic.com" target="_blank"
             matTooltip="Claude API Docs">
            <mat-icon>help_outline</mat-icon>
          </a>
        </mat-toolbar>

        <div class="content-area">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .shell { height: 100vh; }

    /* ── Sidenav ── */
    .sidenav {
      width: 240px;
      background: #0f1117;
      color: white;
      border: none;
      display: flex;
      flex-direction: column;
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 20px 16px 16px;
      border-bottom: 1px solid rgba(255,255,255,.08);
    }
    .brand-icon { color: #818cf8; font-size: 28px; }
    .brand-title { font-weight: 700; font-size: .95rem; line-height: 1.2; }
    .brand-sub { font-size: .7rem; color: rgba(255,255,255,.4); }

    .nav-section-label {
      font-size: .65rem;
      text-transform: uppercase;
      letter-spacing: .1em;
      color: rgba(255,255,255,.3);
      padding: 12px 16px 4px;
    }

    a[mat-list-item] {
      color: rgba(255,255,255,.65);
      border-radius: 6px;
      margin: 2px 8px;
      min-height: 40px;
    }
    a[mat-list-item]:hover { background: rgba(255,255,255,.06); color: white; }

    .active-link {
      background: rgba(129,140,248,.15) !important;
      color: #a5b4fc !important;
    }

    .nav-item-inner {
      display: flex;
      align-items: center;
      gap: 10px;
      width: 100%;
    }

    .stage-chip {
      min-width: 32px;
      height: 20px;
      border-radius: 4px;
      font-size: .65rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .nav-label { font-size: .85rem; }

    .sidenav-footer {
      margin-top: auto;
      padding: 16px;
      border-top: 1px solid rgba(255,255,255,.08);
    }

    .model-chip {
      background: rgba(99,102,241,.2);
      color: #a5b4fc;
      border-radius: 9999px;
      padding: 4px 10px;
      font-size: .7rem;
      font-weight: 600;
      text-align: center;
      font-family: monospace;
    }

    /* ── Main ── */
    .main-content { display: flex; flex-direction: column; background: #f5f6fa; }

    .topbar {
      background: white;
      color: #1a1a2e;
      box-shadow: 0 1px 3px rgba(0,0,0,.08);
      height: 56px;
    }
    .topbar-title { font-weight: 600; font-size: .95rem; }

    .content-area {
      flex: 1;
      overflow-y: auto;
      padding: 24px;
    }
  `]
})
export class AppComponent {
  navItems: NavItem[] = [
    { icon: 'description', label: 'Requirements',   route: '/requirements', stage: 'S1', color: '#6366f1' },
    { icon: 'architecture', label: 'Design',        route: '/design/new',   stage: 'S2', color: '#0ea5e9' },
    { icon: 'code',         label: 'Code Gen',      route: '/codegen/new',  stage: 'S3a', color: '#10b981' },
    { icon: 'rate_review',  label: 'PR Review',     route: '/pr-review',    stage: 'S3b', color: '#f59e0b' },
    { icon: 'science',      label: 'Test Gen',      route: '/testgen/new',  stage: 'S4', color: '#ef4444' },
  ];
}
