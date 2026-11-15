import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ValidationError } from '../models/api.models';

/** A uniform error for the whole app, built from the server's ProblemDetails response. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail: string,
    readonly errors: ValidationError[] = [],
  ) {
    super(detail || title);
  }

  /** Message for the user: the details if there are any, otherwise the title. */
  get userMessage(): string {
    return this.detail || this.title;
  }
}

interface ProblemDetailsBody {
  title?: string;
  detail?: string;
  errors?: ValidationError[] | Record<string, string[]>;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (!(error instanceof HttpErrorResponse)) {
    return new ApiError(0, 'שגיאה', error instanceof Error ? error.message : 'אירעה שגיאה לא צפויה.');
  }
  if (error.status === 0) {
    return new ApiError(0, 'אין חיבור לשרת', 'לא ניתן להתחבר לשרת. יש לוודא שהשרת פועל ולנסות שוב.');
  }

  const body = (typeof error.error === 'object' && error.error ? error.error : {}) as ProblemDetailsBody;
  return new ApiError(
    error.status,
    body.title ?? 'שגיאה',
    body.detail ?? 'אירעה שגיאה בשרת.',
    normalizeErrors(body.errors),
  );
}

/** The server returns a list [{path,message}]; ASP.NET's built-in format is an object {field: [messages]}. Both are supported. */
function normalizeErrors(errors: ProblemDetailsBody['errors']): ValidationError[] {
  if (!errors) return [];
  if (Array.isArray(errors)) return errors;
  return Object.entries(errors).flatMap(([path, messages]) => messages.map((message) => ({ path, message })));
}

/** Interceptor: every HTTP error is translated into an ApiError in one place, so components never deal with the HTTP format. */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(catchError((error: unknown) => throwError(() => toApiError(error))));
