import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { toApiError } from './api/api-error';

/** Short messages to the user (snackbar). A single place so they look the same everywhere. */
@Injectable({ providedIn: 'root' })
export class Notifier {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'סגירה', { duration: 3500, direction: 'rtl' });
  }

  error(error: unknown): void {
    this.snackBar.open(toApiError(error).userMessage, 'סגירה', {
      duration: 7000,
      direction: 'rtl',
      panelClass: 'snack-error',
    });
  }
}
