import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './auth.service';
import { NotesService } from './notes.service';
import { Note, CreateNoteRequest, UpdateNoteRequest } from './models/note.model';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  title = 'Copilot Notepad';
  
  isAuthenticated = false;
  user: any = null;
  notes: Note[] = [];
  selectedNote: Note | null = null;
  isEditing = false;
  isCreating = false;
  
  // Form fields
  noteTitle = '';
  noteContent = '';

  constructor(
    private authService: AuthService,
    private notesService: NotesService
  ) {}

  ngOnInit() {
    this.authService.isAuthenticated$.subscribe(isAuth => {
      this.isAuthenticated = isAuth;
      if (isAuth) {
        this.loadNotes();
      }
    });

    this.authService.user$.subscribe(user => {
      this.user = user;
    });

    // Handle Auth0 callback
    if (window.location.search.includes('code=')) {
      this.authService.handleAuthCallback().subscribe();
    }
  }

  login() {
    this.authService.login().subscribe();
  }

  logout() {
    this.authService.logout();
  }

  loadNotes() {
    this.notesService.getNotes().subscribe({
      next: (notes) => {
        this.notes = notes;
      },
      error: (error) => {
        console.error('Error loading notes:', error);
      }
    });
  }

  selectNote(note: Note) {
    this.selectedNote = note;
    this.noteTitle = note.title;
    this.noteContent = note.content;
    this.isEditing = false;
    this.isCreating = false;
  }

  createNewNote() {
    this.selectedNote = null;
    this.noteTitle = '';
    this.noteContent = '';
    this.isCreating = true;
    this.isEditing = false;
  }

  editNote() {
    this.isEditing = true;
  }

  saveNote() {
    if (this.isCreating) {
      const request: CreateNoteRequest = {
        title: this.noteTitle || 'Untitled',
        content: this.noteContent
      };

      this.notesService.createNote(request).subscribe({
        next: (note) => {
          this.notes.unshift(note);
          this.selectNote(note);
          this.isCreating = false;
        },
        error: (error) => {
          console.error('Error creating note:', error);
        }
      });
    } else if (this.isEditing && this.selectedNote) {
      const request: UpdateNoteRequest = {
        title: this.noteTitle || 'Untitled',
        content: this.noteContent
      };

      this.notesService.updateNote(this.selectedNote.id, request).subscribe({
        next: (updatedNote) => {
          const index = this.notes.findIndex(n => n.id === updatedNote.id);
          if (index >= 0) {
            this.notes[index] = updatedNote;
          }
          this.selectedNote = updatedNote;
          this.isEditing = false;
        },
        error: (error) => {
          console.error('Error updating note:', error);
        }
      });
    }
  }

  deleteNote() {
    if (this.selectedNote) {
      this.notesService.deleteNote(this.selectedNote.id).subscribe({
        next: () => {
          this.notes = this.notes.filter(n => n.id !== this.selectedNote!.id);
          this.selectedNote = null;
          this.noteTitle = '';
          this.noteContent = '';
          this.isEditing = false;
          this.isCreating = false;
        },
        error: (error) => {
          console.error('Error deleting note:', error);
        }
      });
    }
  }

  cancelEdit() {
    if (this.selectedNote) {
      this.noteTitle = this.selectedNote.title;
      this.noteContent = this.selectedNote.content;
    } else {
      this.noteTitle = '';
      this.noteContent = '';
    }
    this.isEditing = false;
    this.isCreating = false;
  }
}
