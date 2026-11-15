import { Pipe, PipeTransform } from '@angular/core';
import { FieldDataType, FieldOption } from '../core/models/api.models';

interface FormattableColumn {
  dataType: FieldDataType;
  unit?: string | null;
  options?: FieldOption[];
}

const numberFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });

/** Displays a value according to the field type: date as dd/MM/yyyy, numbers with separators and a unit, yes/no, and option labels. */
export function formatValue(value: unknown, column: FormattableColumn): string {
  if (value === null || value === undefined || value === '') return '—';

  switch (column.dataType) {
    case 'boolean':
      return value === true || value === 1 || value === 'true' ? 'כן' : 'לא';
    case 'date': {
      const text = String(value);
      const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text);
      return match ? `${match[3]}/${match[2]}/${match[1]}` : text;
    }
    case 'number': {
      const n = typeof value === 'number' ? value : Number(value);
      if (Number.isNaN(n)) return String(value);
      const formatted = numberFormat.format(n);
      return column.unit ? `${formatted} ${column.unit}` : formatted;
    }
    default: {
      const option = column.options?.find((o) => o.value.toLowerCase() === String(value).toLowerCase());
      return option?.label ?? String(value);
    }
  }
}

@Pipe({ name: 'fieldValue' })
export class FieldValuePipe implements PipeTransform {
  transform(value: unknown, column: FormattableColumn): string {
    return formatValue(value, column);
  }
}
