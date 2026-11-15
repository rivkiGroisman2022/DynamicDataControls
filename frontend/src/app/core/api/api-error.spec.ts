import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('reads ProblemDetails with validation errors', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'הבקרה אינה תקינה', detail: 'הגדרת הבקרה אינה תקינה.', errors: [{ path: 'name', message: 'חובה' }] },
      }),
    );

    expect(error.status).toBe(400);
    expect(error.userMessage).toBe('הגדרת הבקרה אינה תקינה.');
    expect(error.errors).toEqual([{ path: 'name', message: 'חובה' }]);
  });

  it('supports the ASP.NET dictionary error format', () => {
    const error = toApiError(new HttpErrorResponse({ status: 400, error: { title: 'x', errors: { name: ['a', 'b'] } } }));
    expect(error.errors).toHaveLength(2);
  });

  it('gives a friendly message when the server is unreachable', () => {
    const error = toApiError(new HttpErrorResponse({ status: 0 }));
    expect(error.title).toBe('אין חיבור לשרת');
  });
});
