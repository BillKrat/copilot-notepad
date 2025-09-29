import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, switchMap } from 'rxjs';
import { Note, CreateNoteRequest, UpdateNoteRequest } from './models/note.model';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class NotesService {
  private apiUrl = 'http://localhost:5000/api/notes'; // This will be the Aspire API URL

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {}

  private getAuthHeaders(): Observable<HttpHeaders> {
    return this.authService.getAccessToken().pipe(
      switchMap(token => {
        const headers = new HttpHeaders({
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        });
        return [headers];
      })
    );
  }

  getNotes(): Observable<Note[]> {
    return this.getAuthHeaders().pipe(
      switchMap(headers => this.http.get<Note[]>(this.apiUrl, { headers }))
    );
  }

  getNote(id: number): Observable<Note> {
    return this.getAuthHeaders().pipe(
      switchMap(headers => this.http.get<Note>(`${this.apiUrl}/${id}`, { headers }))
    );
  }

  createNote(request: CreateNoteRequest): Observable<Note> {
    return this.getAuthHeaders().pipe(
      switchMap(headers => this.http.post<Note>(this.apiUrl, request, { headers }))
    );
  }

  updateNote(id: number, request: UpdateNoteRequest): Observable<Note> {
    return this.getAuthHeaders().pipe(
      switchMap(headers => this.http.put<Note>(`${this.apiUrl}/${id}`, request, { headers }))
    );
  }

  deleteNote(id: number): Observable<void> {
    return this.getAuthHeaders().pipe(
      switchMap(headers => this.http.delete<void>(`${this.apiUrl}/${id}`, { headers }))
    );
  }
}