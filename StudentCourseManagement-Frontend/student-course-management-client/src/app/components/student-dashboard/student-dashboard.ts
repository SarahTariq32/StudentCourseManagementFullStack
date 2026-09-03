import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CourseService } from '../../services/course';
import { StudentService } from '../../services/student';
import { StudentProfile } from '../../models/student.model';
import { Course } from '../../models/course.model';

type StudentView = 'overview' | 'my-courses' | 'available-courses' | 'profile';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './student-dashboard.html',
  styleUrl: './student-dashboard.scss'
})
export class StudentDashboardComponent implements OnInit {
  activeView: StudentView = 'overview';
  
  courses: Course[] = [];
  allSystemCourses: Course[] = []; // Stores all system courses to look up enrolled course IDs
  studentProfile: StudentProfile | null = null;
  isVerified: boolean = false;
  isLoading: boolean = true;
  isEditingProfile: boolean = false;

  profileForm: FormGroup;
  
  accountRequestReason: string = '';
  enrollmentReasonMap: { [courseId: number]: string } = {};
  unenrollmentReasonMap: { [courseName: string]: string } = {};

  statusMessage: string = '';
  errorMessage: string = '';
  showMaxCoursesBanner: boolean = false; // Triggered only on attempt

  get enrolledCount(): number {
    return this.studentProfile?.enrolledCourses?.length || 0;
  }

  get maxCoursesReached(): boolean {
    return this.enrolledCount >= 7;
  }

  constructor(
    private courseService: CourseService,
    private studentService: StudentService,
    private fb: FormBuilder,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.profileForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      age: [20, [Validators.required, Validators.min(16)]]
    });
  }

  ngOnInit(): void {
    this.checkVerificationAndLoadData();
  }

  setView(view: StudentView): void {
    this.activeView = view;
    this.statusMessage = '';
    this.errorMessage = '';
    this.showMaxCoursesBanner = false;
    this.isEditingProfile = false;
  }

  checkVerificationAndLoadData(): void {
    this.isLoading = true;

    this.studentService.getMyProfile().subscribe({
      next: (res) => {
        this.isVerified = res.isVerified;
        if (res.isVerified && res.student) {
          this.studentProfile = res.student;
          this.profileForm.patchValue({
            name: res.student.name,
            email: res.student.email,
            age: res.student.age
          });
        }
        this.loadCourses();
      },
      error: () => {
        this.isVerified = false;
        this.loadCourses();
      }
    });
  }

  loadCourses(): void {
    // Load available courses
    this.courseService.getCourses().subscribe({
      next: (data: any) => {
        this.courses = Array.isArray(data) ? data : (data?.items || []);
        
        // Also fetch all system courses to resolve enrolled course IDs
        this.courseService.getAllCourses().subscribe({
          next: (allData: any) => {
            this.allSystemCourses = Array.isArray(allData) ? allData : (allData?.items || []);
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.allSystemCourses = this.courses;
            this.isLoading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: (err: any) => {
        console.error('Course fetch error:', err);
        this.errorMessage = 'Failed to load course list.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  isAlreadyEnrolled(courseName: string): boolean {
    if (!this.studentProfile?.enrolledCourses) return false;
    return this.studentProfile.enrolledCourses.some(
      c => c.toLowerCase().trim() === courseName.toLowerCase().trim()
    );
  }

  // --- PROFILE EDITING ---
  toggleEditProfile(): void {
    this.isEditingProfile = !this.isEditingProfile;
    if (this.studentProfile) {
      this.profileForm.patchValue({
        name: this.studentProfile.name,
        email: this.studentProfile.email,
        age: this.studentProfile.age
      });
    }
  }

  onSaveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.studentService.updateMyProfile(this.profileForm.value).subscribe({
      next: () => {
        this.statusMessage = 'Profile updated successfully!';
        this.isEditingProfile = false;
        this.checkVerificationAndLoadData();
      },
      error: (err: any) => {
        this.errorMessage = err.error?.message || 'Failed to update profile details.';
      }
    });
  }

  // --- ENROLLMENT & UNENROLLMENT ACTIONS ---
  onEnrollDirectly(courseId: number): void {
    this.statusMessage = '';
    this.errorMessage = '';

    if (this.maxCoursesReached) {
      this.showMaxCoursesBanner = true;
      return;
    }

    this.studentService.enrollDirectly(courseId).subscribe({
      next: (res: any) => {
        this.statusMessage = res.message || 'Successfully enrolled in course!';
        this.checkVerificationAndLoadData();
      },
      error: (err: any) => {
        this.errorMessage = err.error?.message || err.error || 'Enrollment failed.';
      }
    });
  }

  onRequestEnrollment(courseId: number): void {
    this.statusMessage = '';
    this.errorMessage = '';

    if (this.maxCoursesReached) {
      this.showMaxCoursesBanner = true;
      return;
    }

    const matchedCourse = this.courses.find(c => c.id === courseId);
    const courseName = matchedCourse ? matchedCourse.name : '';
    const reason = this.enrollmentReasonMap[courseId] || 'Special enrollment permission requested';

    this.studentService.requestEnrollment(courseId, reason, this.studentProfile?.id, courseName).subscribe({
      next: (res: any) => {
        this.statusMessage = res?.message || 'Enrollment request submitted to Admin for approval.';
        this.enrollmentReasonMap[courseId] = '';
      },
      error: (err: any) => {
        this.errorMessage = typeof err.error === 'string' ? err.error : (err.error?.message || 'Failed to submit enrollment request.');
      }
    });
  }

  onRequestUnenrollment(courseName: string): void {
    this.statusMessage = '';
    this.errorMessage = '';
    this.showMaxCoursesBanner = false;

    const reason = this.unenrollmentReasonMap[courseName] || 'Student requested unenrollment';

    // Pass courseName cleanly
    this.studentService.requestUnenrollment(0, reason, this.studentProfile?.id, courseName.trim()).subscribe({
      next: (res: any) => {
        this.statusMessage = res?.message || `Unenrollment request for "${courseName}" sent to Admin!`;
        this.unenrollmentReasonMap[courseName] = '';
        this.checkVerificationAndLoadData();
      },
      error: (err: any) => {
        console.error('Unenrollment request failed:', err);
        if (typeof err.error === 'string') {
          this.errorMessage = err.error;
        } else if (err.error?.message) {
          this.errorMessage = err.error.message;
        } else {
          this.errorMessage = 'Failed to submit unenrollment request.';
        }
      }
    });
  }

  onRequestAccountCreation(): void {
    const reason = this.accountRequestReason || 'Student requested account verification/registration';
    this.statusMessage = '';
    this.errorMessage = '';

    this.studentService.requestAccountCreation(reason).subscribe({
      next: (res: any) => {
        this.statusMessage = res?.message || 'Student registration request submitted to Admin successfully!';
        this.accountRequestReason = '';
      },
      error: (err: any) => {
        this.errorMessage = typeof err.error === 'string' ? err.error : (err.error?.message || 'Failed to submit registration request.');
      }
    });
  }

  onLogout(): void {
    localStorage.clear();
    this.router.navigate(['/login']);
  }
}