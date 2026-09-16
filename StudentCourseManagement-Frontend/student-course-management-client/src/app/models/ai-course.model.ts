export interface RecommendedCourse {
  id: number;
  name: string;
  reason: string;
}

export interface CourseRecommendationResponse {
  matchedCourses: RecommendedCourse[];
  advisorNote: string;
}