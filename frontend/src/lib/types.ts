// Mirrors the API DTOs (see docs/api.md). camelCase JSON.

export interface ApiEnvelope<T> {
  data: T;
  message: string | null;
  errors: string[];
  success: boolean;
  pagination?: { page: number; pageSize: number; total: number; totalPages: number } | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface University { id: string; name: string; slug: string; code?: string | null; city?: string | null; country: string; isActive: boolean; }
export interface Faculty { id: string; name: string; slug: string; code?: string | null; universityId: string; isActive: boolean; }
export interface Department { id: string; name: string; slug: string; code?: string | null; facultyId: string; isActive: boolean; }
export interface Specialty { id: string; name: string; slug: string; code?: string | null; description?: string | null; departmentId: string; domainId?: string | null; isActive: boolean; }
export interface Level { id: string; name: string; slug: string; shortName: string; cycle: string; order: number; specialtyId: string; isActive: boolean; }
export interface Semester { id: string; name: string; slug: string; shortName: string; order: number; levelId: string; isActive: boolean; }
export interface AcademicYear { id: string; name: string; slug: string; startYear: number; endYear: number; isCurrent: boolean; isActive: boolean; }
export interface Session { id: string; name: string; slug: string; kind: string; order: number; isActive: boolean; }
export interface Module {
  id: string; name: string; slug: string; code?: string | null; description?: string | null;
  coefficient: number; credits: number; semesterId: string; specialtyId: string; isActive: boolean;
}

export type DocumentType =
  | "Course" | "TD" | "TP" | "Exam" | "ExamSolution" | "Test" | "TestSolution"
  | "Exercise" | "ExerciseSolution" | "Summary" | "Other";

export interface DocumentDto {
  id: string;
  title: string;
  slug: string;
  description?: string | null;
  type: DocumentType;
  status: string;
  moduleId: string | null;
  academicYearId?: string | null;
  sessionId?: string | null;
  fileName: string;
  fileSize: number;
  pageCount?: number | null;
  mimeType: string;
  isPremium: boolean;
  source?: string | null;
  rightsStatus: string;
  hasPreview: boolean;
  viewCount: number;
  downloadCount: number;
  solutionForDocumentId?: string | null;
  solutionDocumentIds: string[];
  publishedAt?: string | null;
  createdAt: string;
  classificationStatus?: string;
  verificationStatus?: string;
}

export interface SearchHit {
  id: string;
  title: string;
  slug: string;
  type: DocumentType;
  moduleName: string;
  moduleSlug: string;
  specialtyName?: string | null;
  year?: number | null;
  session?: string | null;
  isPremium: boolean;
  hasPreview: boolean;
}

/* ---------- community classification ---------- */

export interface QuotaStatus {
  enabled: boolean;
  exempt: boolean;
  allowed: number;
  used: number;
  remaining: number;
  bonusEarned: number;
  resetsAt: string | null;
  bonusPerContribution: number;
  totalDownloads: number;
  validContributions: number;
  documentsPerTask: number;
}

export interface ClassifyPrompt {
  shouldPrompt: boolean;
  reason: "login" | "downloads" | null;
  hasOpenTask: boolean;
  openTaskRemaining: number;
  documentsPerTask: number;
  availableDocuments: number;
  quota: QuotaStatus;
}

export interface TaskItem {
  assignmentId: string;
  documentId: string;
  status: "Assigned" | "Completed" | "Skipped" | "Expired";
  title: string;
  fileName: string;
  fileSize: number;
  pageCount: number | null;
  mimeType: string;
  hasPreview: boolean;
  description: string | null;
  source: string | null;
  uploadedAt: string;
}

export interface ClassificationTask {
  id: string;
  trigger: string;
  expiresAt: string;
  total: number;
  resolved: number;
  items: TaskItem[];
}

export interface TaxonomyOption {
  id: string;
  name: string;
  parentId: string | null;
  pending?: boolean;
  proposalId?: string | null;
}

export interface ClassificationOptions {
  departments: TaxonomyOption[];
  specialties: TaxonomyOption[];
  academicYears: TaxonomyOption[];
  sessions: TaxonomyOption[];
  documentTypes: { value: string; label: string }[];
  myPendingProposals: { id: string; name: string; category: string; parentId: string | null }[];
}

export interface VoteResult {
  documentId: string;
  outcome: string;
  rewarded: boolean;
  bonusDownloadsGranted: number;
  remaining: number;
  taskCompleted: boolean;
  quota: QuotaStatus;
}
