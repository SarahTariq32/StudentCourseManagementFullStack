export interface Course {
  id: number;
  name: string;
  credits: number;
  enrolledStudentsCount: number;
}

export interface PagedResult<T> {
  items: T[];
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}