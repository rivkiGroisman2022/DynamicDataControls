import { TestBed } from '@angular/core/testing';
import { ControlRunResult } from '../../../core/models/api.models';
import { RunResults } from './run-results';

const result: ControlRunResult = {
  controlId: 1,
  controlName: 'שכר לא תקין',
  entityKey: 'Employee',
  entityDisplayName: 'עובדים',
  definitionText: 'עובדים – שכר חודשי קטן מ־0 או גיל קטן מ־18',
  totalViolations: 3,
  returnedRows: 3,
  isTruncated: false,
  durationMs: 12,
  runAt: '2026-11-11T10:00:00',
  columns: [
    { key: 'salary', displayName: 'שכר חודשי', groupLabel: 'עובד', dataType: 'number', unit: 'ש"ח', isRuleField: true, options: [] },
    { key: 'age', displayName: 'גיל', groupLabel: 'עובד', dataType: 'number', isRuleField: true, options: [] },
  ],
  rules: [
    { index: 0, text: 'שכר חודשי קטן מ־0' },
    { index: 1, text: 'גיל קטן מ־18' },
  ],
  rows: [
    { recordId: '1', title: 'דנה כהן', values: { salary: -5, age: 30 }, reasons: ['שכר חודשי: -5'], matchedRules: [0] },
    { recordId: '2', title: 'אלון חדד', values: { salary: 100, age: 16 }, reasons: ['גיל: 16'], matchedRules: [1] },
    { recordId: '3', title: 'ליאור לוי', values: { salary: -1, age: 15 }, reasons: ['שכר', 'גיל'], matchedRules: [0, 1] },
  ],
};

describe('RunResults', () => {
  function create() {
    const fixture = TestBed.createComponent(RunResults);
    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();
    // Access protected members in a test - read-only use, to check the derived state.
    const component = fixture.componentInstance as unknown as Record<string, any>;
    return { fixture, component };
  }

  it('counts violations per rule for the reason filter', () => {
    const { component } = create();
    expect(component['ruleCounts']().map((r: { count: number }) => r.count)).toEqual([2, 2]);
  });

  it('filters rows by selected reason', () => {
    const { component } = create();
    component['setRuleFilter']([1]);
    expect(component['filtered']().map((r: { recordId: string }) => r.recordId)).toEqual(['2', '3']);
  });

  it('searches in titles and formatted values', () => {
    const { component } = create();
    component['setSearch']('אלון');
    expect(component['filtered']()).toHaveLength(1);
  });

  it('sorts numerically', () => {
    const { component } = create();
    component['sort'].set({ active: 'age', direction: 'asc' });
    expect(component['sorted']().map((r: { recordId: string }) => r.recordId)).toEqual(['3', '2', '1']);
  });

  it('renders a row per violation with its reason', async () => {
    const { fixture } = create();
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('tr.mat-mdc-row')).toHaveLength(3);
    expect(element.textContent).toContain('גיל: 16');
  });
});
