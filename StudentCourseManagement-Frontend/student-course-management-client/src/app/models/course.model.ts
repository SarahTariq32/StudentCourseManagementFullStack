// export interface Course {
//   id: number;
//   title: string;
//   code: string;
//   description: string;
// }

// // Matches backend GetPagedAsync response wrapper
// export interface PagedResult<T> {
//   items: T[];
//   totalCount: number;
//   pageNumber: number;
//   pageSize: number;
// }

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