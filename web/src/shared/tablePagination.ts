export const TABLE_ROWS = 10
export const TABLE_ROWS_OPTIONS = [10, 20, 50]

export type DataTablePageEvent = {
  page: number
  first: number
  rows: number
}

export function tableFirst(page: number, pageSize: number) {
  return Math.max(0, (page - 1) * pageSize)
}
