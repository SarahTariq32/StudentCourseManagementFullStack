export interface PendingRequest {
  requestId: number;
  studentId: number;
  studentName: string;
  courseId: number;
  courseName: string;
  requestType: string;
  reason: string;
}