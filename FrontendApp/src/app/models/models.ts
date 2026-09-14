// Mirrors the API DTOs. Keeping them in one file makes the contract easy to
// diff against the backend when something changes.

export type UserRole = 'Librarian' | 'Member';

export interface AuthResponse {
  token: string;
  expiresAt: string;
  role: UserRole;
  name: string;
  email: string;
  memberId: number | null;
}

export interface ApiError {
  status: number;
  message: string;
  errors: Record<string, string[]> | null;
}

export interface Book {
  id: number;
  title: string;
  isbn: string;
  categoryId: number;
  categoryName: string;
  totalCopies: number;
  availableCopies: number;
  isAvailable: boolean;
  coverImagePath: string | null;
}

export interface BookDetail extends Book {
  description: string | null;
  publishedYear: number | null;
  authors: string[];
}

export interface Member {
  id: number;
  fullName: string;
  email: string;
  phone: string | null;
  joinedOn: string;
  isActive: boolean;
  activeLoans: number;
}

export type RequestStatus = 'Pending' | 'Approved' | 'Rejected';

export interface IssueRequest {
  id: number;
  bookId: number;
  bookTitle: string;
  memberId: number;
  memberName: string;
  requestedOn: string;
  status: RequestStatus;
  rejectionReason: string | null;
  decidedOn: string | null;
  issuedBookId: number | null;
}

export interface IssuedBook {
  id: number;
  bookId: number;
  bookTitle: string;
  bookIsbn: string;
  memberId: number;
  memberName: string;
  issuedDate: string;
  dueDate: string;
  returnDate: string | null;
  isReturned: boolean;
  reIssueCount: number;
  reIssuesRemaining: number;
  fine: number;
  overdueDays: number;
  isOverdue: boolean;
  cooldownUntil: string;
}

export interface PopularBook {
  bookId: number;
  title: string;
  timesIssued: number;
  averageRating: number | null;
}

export interface Dashboard {
  totalBooks: number;
  totalCopies: number;
  availableCopies: number;
  totalMembers: number;
  activeMembers: number;
  pendingRequests: number;
  outstandingLoans: number;
  overdueLoans: number;
  outstandingFines: number;
  unresolvedProblems: number;
  popularBooks: PopularBook[];
}

export interface BookProblem {
  id: number;
  bookId: number;
  bookTitle: string;
  memberId: number;
  memberName: string;
  problemType: string;
  problemLabel: string;
  description: string | null;
  isResolved: boolean;
  reportedOn: string;
  resolvedOn: string | null;
}

export interface BookMemberStatus {
  bookId: number;
  canRequest: boolean;
  reason: string | null;
  cooldownUntil: string | null;
  currentlyHeld: boolean;
  hasPendingRequest: boolean;
  issuedBookId: number | null;
  dueDate: string | null;
  daysUntilDue: number | null;
  isOverdue: boolean;
  reIssuesRemaining: number;
  hasEverBorrowed: boolean;
}

export interface Feedback {
  id: number;
  bookId: number;
  bookTitle: string;
  memberId: number;
  memberName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
}

export interface Quote {
  id: number;
  bookId: number;
  bookTitle: string;
  memberId: number;
  memberName: string;
  text: string | null;
  imagePath: string | null;
  likes: number;
  createdAt: string;
}

export interface Category {
  id: number;
  name: string;
}

export interface Affinity {
  name: string;
  weight: number;
  count: number;
}

export interface TasteProfile {
  booksBorrowed: number;
  booksRated: number;
  topCategories: Affinity[];
  topAuthors: Affinity[];
}

export interface Recommendation {
  bookId: number;
  title: string;
  isbn: string;
  categoryName: string;
  authors: string[];
  coverImagePath: string | null;
  availableCopies: number;
  isAvailable: boolean;
  averageRating: number | null;
  timesIssued: number;
  reason: string;
  score: number;
}

export interface RecommendationResponse {
  isColdStart: boolean;
  profile: TasteProfile;
  recommendations: Recommendation[];
}

export type TrendDirection = 'New' | 'Rising' | 'Steady' | 'Falling';

export interface TrendingBook {
  bookId: number;
  title: string;
  isbn: string;
  categoryName: string;
  authors: string[];
  coverImagePath: string | null;
  availableCopies: number;
  totalCopies: number;
  issuesInWindow: number;
  issuesInPreviousWindow: number;
  change: number;
  percentChange: number | null;
  distinctMembers: number;
  requestsInWindow: number;
  trend: TrendDirection;
  score: number;
}

export interface TrendingGroup {
  id: number;
  name: string;
  issuesInWindow: number;
  issuesInPreviousWindow: number;
  change: number;
  percentChange: number | null;
  distinctMembers: number;
  titleCount: number;
  topTitle: string | null;
  trend: TrendDirection;
  score: number;
}

export interface TrendingResponse {
  windowDays: number;
  windowStart: string;
  previousWindowStart: string;
  totalIssuesInWindow: number;
  totalIssuesInPreviousWindow: number;
  books: TrendingBook[];
  categories: TrendingGroup[];
  authors: TrendingGroup[];
}
