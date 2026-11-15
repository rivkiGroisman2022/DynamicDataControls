import { formatValue } from './value-format';

describe('formatValue', () => {
  it('formats dates as dd/MM/yyyy', () => {
    expect(formatValue('2016-02-28T00:00:00', { dataType: 'date' })).toBe('28/02/2016');
  });

  it('formats numbers with separators and unit', () => {
    expect(formatValue(17000, { dataType: 'number', unit: 'ש"ח' })).toBe('17,000 ש"ח');
    expect(formatValue(-1200, { dataType: 'number' })).toBe('-1,200');
  });

  it('formats booleans in Hebrew', () => {
    expect(formatValue(true, { dataType: 'boolean' })).toBe('כן');
    expect(formatValue(false, { dataType: 'boolean' })).toBe('לא');
  });

  it('shows the option label instead of the stored code', () => {
    expect(formatValue('Active', { dataType: 'string', options: [{ value: 'Active', label: 'פעיל' }] })).toBe('פעיל');
  });

  it('shows a dash for empty values', () => {
    expect(formatValue(null, { dataType: 'string' })).toBe('—');
  });
});
