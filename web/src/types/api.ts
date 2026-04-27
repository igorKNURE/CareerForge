/**
 * Wire-format types mirroring the C# DTOs returned by the CareerForge API.
 * Keep these in sync with `src/CareerForge.Api/Endpoints/*` and `CareerForge.Application/**`.
 */

/** Stages of the resume / job-description ingestion pipeline. */
export type ProcessingStatus =
  | 'Pending' | 'Parsing' | 'Chunking' | 'Embedding' | 'Summarizing' | 'Done' | 'Error';

/** Severity of a match-report finding. */
export type FindingSeverity = 'Critical' | 'Warning' | 'Info';
/** Topical category of an interview question. */
export type QuestionCategory = 'Behavioral' | 'Resume' | 'Technical' | 'SystemDesign' | 'Coding' | 'Other';
/** Relative difficulty of a generated interview question. */
export type QuestionDifficulty = 'Easy' | 'Medium' | 'Hard';
/** Suggested rhetorical structure for an interview answer. */
export type AnswerFormat = 'STAR' | 'StructuredBullets' | 'StepByStep' | 'Freeform';

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
}

export interface MeResponse {
  id: string;
  email: string;
  displayName: string | null;
}

export interface ResumeListItem {
  id: string;
  fileName: string;
  status: ProcessingStatus;
  createdAt: string;
  updatedAt: string;
}

export interface ResumeProfile {
  fullName: string;
  headline: string;
  summary: string;
  yearsOfExperience?: number;
  skills: string[];
  experience: Array<{
    company: string;
    role: string;
    startDate?: string;
    endDate?: string;
    highlights: string[];
  }>;
  education: Array<{ institution: string; degree: string; year?: string }>;
}

export interface ResumeResponse {
  id: string;
  fileName: string;
  status: ProcessingStatus;
  summary: string | null;
  profile: ResumeProfile | null;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface VacancyListItem {
  id: string;
  title: string;
  company: string | null;
  status: ProcessingStatus;
  createdAt: string;
  updatedAt: string;
}

export interface JobProfile {
  title: string;
  company?: string | null;
  seniority?: string | null;
  yearsRequired?: number | null;
  summary: string;
  mustHaveSkills: string[];
  niceToHaveSkills: string[];
  responsibilities: string[];
  qualifications: string[];
}

export interface VacancyResponse {
  id: string;
  title: string;
  company: string | null;
  status: ProcessingStatus;
  summary: string | null;
  profile: JobProfile | null;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface MatchFinding {
  severity: FindingSeverity;
  category: string;
  title: string;
  description: string;
  recommendation: string | null;
  resumeExcerpt: string | null;
}

export interface MatchReportListItem {
  id: string;
  resumeId: string;
  jobDescriptionId: string;
  overallScore: number;
  createdAt: string;
  criticalCount: number;
  warningCount: number;
  infoCount: number;
}

export interface MatchReportResponse {
  id: string;
  resumeId: string;
  jobDescriptionId: string;
  overallScore: number;
  skillCoverageScore: number;
  semanticSimilarityScore: number;
  experienceFitScore: number;
  matchedMustHaveSkills: string[] | null;
  missingMustHaveSkills: string[] | null;
  matchedNiceToHaveSkills: string[] | null;
  findings: MatchFinding[];
  improvementSummary: string | null;
  createdAt: string;
}

export interface SessionListItem {
  id: string;
  name: string;
  language: string;
  resumeId: string;
  jobDescriptionId: string;
  turnCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface AnswerEvaluation {
  contentScore: number;
  structureScore: number;
  relevanceScore: number;
  overallScore: number;
  strengths: string;
  weaknesses: string;
  recommendations: string[];
  evaluationFailed?: boolean;
}

export interface TurnResponse {
  id: string;
  turnIndex: number;
  questionText: string;
  category: QuestionCategory;
  difficulty: QuestionDifficulty;
  expectedFormat: AnswerFormat;
  rationale: string | null;
  answerText: string | null;
  evaluation: AnswerEvaluation | null;
  createdAt: string;
  answeredAt: string | null;
}

export interface SessionResponse {
  id: string;
  name: string;
  language: string;
  resumeId: string;
  jobDescriptionId: string;
  turnCount: number;
  createdAt: string;
  updatedAt: string;
  turns: TurnResponse[];
}
