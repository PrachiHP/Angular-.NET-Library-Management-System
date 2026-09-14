import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  Book, BookDetail, BookMemberStatus, BookProblem, Dashboard, Feedback,
  IssueRequest, IssuedBook, Member, Quote, RecommendationResponse, TrendingResponse,
} from '../models/models';

/** Builds HttpParams from a plain object, skipping empty values. */
function toParams(source: Record<string, unknown>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(source)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

@Injectable({ providedIn: 'root' })
export class BookApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/books`;

  list(filters: { search?: string; categoryId?: number; onlyAvailable?: boolean } = {}): Observable<Book[]> {
    return this.http.get<Book[]>(this.url, { params: toParams(filters) });
  }

  get(id: number): Observable<BookDetail> {
    return this.http.get<BookDetail>(`${this.url}/${id}`);
  }

  create(body: unknown): Observable<BookDetail> {
    return this.http.post<BookDetail>(this.url, body);
  }

  update(id: number, body: unknown): Observable<BookDetail> {
    return this.http.put<BookDetail>(`${this.url}/${id}`, body);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class MemberApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/members`;

  list(filters: { search?: string; onlyActive?: boolean } = {}): Observable<Member[]> {
    return this.http.get<Member[]>(this.url, { params: toParams(filters) });
  }

  deactivate(id: number): Observable<void> {
    return this.http.patch<void>(`${this.url}/${id}/deactivate`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class IssueApi {
  private readonly http = inject(HttpClient);
  private readonly requests = `${environment.apiUrl}/issue-requests`;
  private readonly issued = `${environment.apiUrl}/issued`;

  request(bookId: number): Observable<IssueRequest> {
    return this.http.post<IssueRequest>(this.requests, { bookId });
  }

  allRequests(status?: string): Observable<IssueRequest[]> {
    return this.http.get<IssueRequest[]>(this.requests, { params: toParams({ status }) });
  }

  myRequests(): Observable<IssueRequest[]> {
    return this.http.get<IssueRequest[]>(`${this.requests}/my`);
  }

  approve(id: number): Observable<IssueRequest> {
    return this.http.patch<IssueRequest>(`${this.requests}/${id}/approve`, {});
  }

  reject(id: number, reason: string): Observable<IssueRequest> {
    return this.http.patch<IssueRequest>(`${this.requests}/${id}/reject`, { reason });
  }

  allLoans(onlyOutstanding?: boolean): Observable<IssuedBook[]> {
    return this.http.get<IssuedBook[]>(this.issued, { params: toParams({ onlyOutstanding }) });
  }

  overdue(): Observable<IssuedBook[]> {
    return this.http.get<IssuedBook[]>(`${this.issued}/overdue`);
  }

  myLoans(onlyOutstanding?: boolean): Observable<IssuedBook[]> {
    return this.http.get<IssuedBook[]>(`${this.issued}/my`, { params: toParams({ onlyOutstanding }) });
  }

  return_(id: number): Observable<IssuedBook> {
    return this.http.patch<IssuedBook>(`${this.issued}/${id}/return`, {});
  }

  reissue(id: number): Observable<IssuedBook> {
    return this.http.patch<IssuedBook>(`${this.issued}/${id}/reissue`, {});
  }

  exportUrl(): string {
    return `${this.issued}/export`;
  }
}

@Injectable({ providedIn: 'root' })
export class LibraryApi {
  private readonly http = inject(HttpClient);

  dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${environment.apiUrl}/dashboard`);
  }

  problems(onlyUnresolved?: boolean): Observable<BookProblem[]> {
    return this.http.get<BookProblem[]>(`${environment.apiUrl}/book-problems`, {
      params: toParams({ onlyUnresolved }),
    });
  }

  resolveProblem(id: number): Observable<BookProblem> {
    return this.http.patch<BookProblem>(`${environment.apiUrl}/book-problems/${id}/resolve`, {});
  }

  reportProblem(body: { bookId: number; problemType: string; description?: string }): Observable<BookProblem> {
    return this.http.post<BookProblem>(`${environment.apiUrl}/book-problems`, body);
  }
}

@Injectable({ providedIn: 'root' })
export class EngagementApi {
  private readonly http = inject(HttpClient);

  memberStatus(bookId: number): Observable<BookMemberStatus> {
    return this.http.get<BookMemberStatus>(`${environment.apiUrl}/books/${bookId}/member-status`);
  }

  /**
   * Cover upload uses FormData, not JSON. Do NOT set a Content-Type header —
   * the browser must set it itself so it can append the multipart boundary.
   */
  uploadCover(bookId: number, file: File): Observable<BookDetail> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<BookDetail>(`${environment.apiUrl}/books/${bookId}/cover`, form);
  }

  feedbackForBook(bookId: number): Observable<Feedback[]> {
    return this.http.get<Feedback[]>(`${environment.apiUrl}/feedback/book/${bookId}`);
  }

  addFeedback(body: { bookId: number; rating: number; comment?: string }): Observable<Feedback> {
    return this.http.post<Feedback>(`${environment.apiUrl}/feedback`, body);
  }

  quotesForBook(bookId: number): Observable<Quote[]> {
    return this.http.get<Quote[]>(`${environment.apiUrl}/quotes/book/${bookId}`);
  }

  addQuote(bookId: number, text: string | null, image: File | null): Observable<Quote> {
    const form = new FormData();
    form.append('bookId', String(bookId));
    if (text) form.append('text', text);
    if (image) form.append('image', image);
    return this.http.post<Quote>(`${environment.apiUrl}/quotes`, form);
  }

  likeQuote(id: number): Observable<Quote> {
    return this.http.patch<Quote>(`${environment.apiUrl}/quotes/${id}/like`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class RecommendationApi {
  private readonly http = inject(HttpClient);

  forMe(limit = 10): Observable<RecommendationResponse> {
    return this.http.get<RecommendationResponse>(`${environment.apiUrl}/recommendations`, {
      params: toParams({ limit }),
    });
  }
}

@Injectable({ providedIn: 'root' })
export class TrendingApi {
  private readonly http = inject(HttpClient);

  get(days = 30, limit = 10): Observable<TrendingResponse> {
    return this.http.get<TrendingResponse>(`${environment.apiUrl}/trending`, {
      params: toParams({ days, limit }),
    });
  }
}
