import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// ── Shared models ─────────────────────────────────────────────────────────────

export interface RequirementDocument {
  id: string;
  title: string;
  summary: string;
  status: RequirementStatus;
  rawInput: string;
  tokensUsed: number;
  modelVersion: string;
  adoWorkItemId?: number;
  adoWorkItemUrl?: string;
  createdAt: string;
  acceptanceCriteria: AcceptanceCriterion[];
  edgeCases: EdgeCase[];
  dependencies: Dependency[];
  riskFlags: RiskFlag[];
  effortEstimate?: EffortEstimate;
  conversationHistory: ConversationTurn[];
  designArtifacts: DesignArtifact[];
}

export enum RequirementStatus {
  Draft = 0, Analyzed = 1, Approved = 2, InDesign = 3, InDev = 4, Done = 5
}

export interface AcceptanceCriterion { id: string; description: string; isTestable: boolean; orderIndex: number; }
export interface EdgeCase { id: string; description: string; severity: 'low' | 'medium' | 'high'; mitigationNote?: string; }
export interface Dependency { id: string; name: string; type: string; notes?: string; }
export interface RiskFlag { id: string; description: string; severity: 'low' | 'medium' | 'high'; mitigation?: string; }
export interface EffortEstimate { storyPoints: number; confidence: string; estimatedDaysLow: number; estimatedDaysHigh: number; rationale?: string; }
export interface ConversationTurn { id: string; role: 'user' | 'assistant'; content: string; turnIndex: number; tokensUsed?: number; }

export interface DesignArtifact {
  id: string;
  requirementDocumentId: string;
  type: DesignArtifactType;
  title: string;
  content: string;
  format: 'json' | 'yaml' | 'markdown';
  tokensUsed: number;
  modelVersion: string;
  createdAt: string;
}

export enum DesignArtifactType { ComponentTree = 1, ApiContract = 2, DataModel = 3 }

export interface CodeGenResult {
  id: string;
  requirementDocumentId: string;
  language: CodeGenLanguage;
  generatedContent: string;
  fileName: string;
  tokensUsed: number;
  modelVersion: string;
  adoCommitUrl?: string;
  adoBranch?: string;
  createdAt: string;
}

export enum CodeGenLanguage { CSharp = 1, Angular = 2, Both = 3 }

export interface TestGenResult {
  id: string;
  sourceFile: string;
  target: TestGenTarget;
  generatedContent: string;
  outputFileName: string;
  tokensUsed: number;
  modelVersion: string;
  adoCommitUrl?: string;
  createdAt: string;
}

export enum TestGenTarget { DotNet = 1, Angular = 2, Both = 3 }

export interface PrReviewResult {
  id: string;
  adoProject: string;
  repository: string;
  pullRequestId: number;
  commentsPosted: number;
  tokensUsed: number;
  modelVersion: string;
  reviewJson: string;
  createdAt: string;
}

export interface UsageSummary {
  stage: string;
  totalCalls: number;
  totalTokens: number;
  avgDurationMs: number;
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class PdlcApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.pdlcApiUrl;

  // ── Stage 1: Requirements ──────────────────────────────────

  analyzeRequirement(rawInput: string, adoProject?: string, syncToAdo = false): Observable<RequirementDocument> {
    return this.http.post<RequirementDocument>(`${this.base}/api/pdlc/requirements/analyze`, {
      rawInput,
      createdBy: 'pdlc-console',
      adoProject: adoProject ?? null,
      syncToAdo,
      promptVersion: 'v1',
    });
  }

  refineRequirement(id: string, message: string): Observable<ConversationTurn> {
    return this.http.post<ConversationTurn>(`${this.base}/api/pdlc/requirements/${id}/refine`, { message });
  }

  syncRequirementToAdo(id: string, adoProject: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/pdlc/requirements/${id}/sync-ado`, { adoProject });
  }

  getRequirement(id: string): Observable<RequirementDocument> {
    return this.http.get<RequirementDocument>(`${this.base}/api/pdlc/requirements/${id}`);
  }

  listRequirements(page = 1, pageSize = 20): Observable<RequirementDocument[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<RequirementDocument[]>(`${this.base}/api/pdlc/requirements`, { params });
  }

  getConversation(id: string): Observable<ConversationTurn[]> {
    return this.http.get<ConversationTurn[]>(`${this.base}/api/pdlc/requirements/${id}/conversation`);
  }

  // ── Stage 2: Design ────────────────────────────────────────

  generateDesign(requirementDocumentId: string): Observable<DesignArtifact[]> {
    return this.http.post<DesignArtifact[]>(`${this.base}/api/pdlc/design/generate`, {
      requirementDocumentId,
      createdBy: 'pdlc-console',
      promptVersion: 'v1',
    });
  }

  getDesignArtifacts(requirementId: string): Observable<DesignArtifact[]> {
    return this.http.get<DesignArtifact[]>(`${this.base}/api/pdlc/design/${requirementId}`);
  }

  regenerateArtifact(requirementId: string, type: DesignArtifactType, additionalContext?: string): Observable<DesignArtifact> {
    return this.http.post<DesignArtifact>(`${this.base}/api/pdlc/design/${requirementId}/regenerate`, {
      type,
      additionalContext: additionalContext ?? null,
    });
  }

  // ── Stage 3a: Code Generation ──────────────────────────────

  generateCode(requirementDocumentId: string, language = CodeGenLanguage.Both): Observable<CodeGenResult[]> {
    return this.http.post<CodeGenResult[]>(`${this.base}/api/pdlc/codegen/generate`, {
      requirementDocumentId,
      language,
      createdBy: 'pdlc-console',
      promptVersion: 'v1',
    });
  }

  getCodeGenResults(requirementId: string): Observable<CodeGenResult[]> {
    return this.http.get<CodeGenResult[]>(`${this.base}/api/pdlc/codegen/${requirementId}`);
  }

  commitCodeToAdo(requirementId: string, adoProject: string, repository: string, branch: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/pdlc/codegen/${requirementId}/commit-ado`, {
      adoProject, repository, branch,
    });
  }

  // ── Stage 3b: PR Review ────────────────────────────────────

  reviewPullRequest(adoProject: string, repository: string, pullRequestId: number, postToAdo = true): Observable<PrReviewResult> {
    return this.http.post<PrReviewResult>(`${this.base}/api/pdlc/pr-review/review`, {
      adoProject, repository, pullRequestId,
      promptVersion: 'v1',
      postCommentsToAdo: postToAdo,
    });
  }

  // ── Stage 4: Test Generation ───────────────────────────────

  generateTests(sourceContent: string, sourceFileName: string, target = TestGenTarget.Both, requirementDocumentId?: string): Observable<TestGenResult[]> {
    return this.http.post<TestGenResult[]>(`${this.base}/api/pdlc/testgen/generate`, {
      sourceContent, sourceFileName, target,
      requirementDocumentId: requirementDocumentId ?? null,
      createdBy: 'pdlc-console',
      promptVersion: 'v1',
    });
  }

  // ── Observability ──────────────────────────────────────────

  getUsageSummary(): Observable<UsageSummary[]> {
    return this.http.get<UsageSummary[]>(`${this.base}/api/pdlc/usage`);
  }
}
