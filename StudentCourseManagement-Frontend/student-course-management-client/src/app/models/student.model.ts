export interface StudentProfile {
  id: number;
  name: string;
  email: string;
  age: number;
  enrolledCourses: string[];
}

export interface VerificationResponse {
  isVerified: boolean;
  message?: string;
  student?: StudentProfile;
}

export interface UpdateStudentProfileRequest {
  name: string;
  email: string;
  age: number;
}