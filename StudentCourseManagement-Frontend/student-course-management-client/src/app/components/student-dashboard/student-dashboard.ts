import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CourseService } from '../../services/course';
import { StudentService, StudentProfile } from '../../services/student';
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
    this.courseService.getCourses().subscribe({
      next: (data: any) => {
        this.courses = Array.isArray(data) ? data : (data?.items || []);
        this.isLoading = false;
        this.cdr.detectChanges();
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
    if (this.profileForm.invalid) return;

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
    const reason = this.enrollmentReasonMap[courseId] || 'Special enrollment permission requested';
    this.statusMessage = '';
    this.errorMessage = '';

    this.studentService.requestEnrollment(courseId, reason).subscribe({
      next: () => {
        this.statusMessage = 'Enrollment request submitted to Admin for approval.';
        this.enrollmentReasonMap[courseId] = '';
      },
      error: (err: any) => {
        this.errorMessage = err.error?.message || 'Failed to submit enrollment request.';
      }
    });
  }

  onRequestUnenrollment(courseName: string): void {
  const matchedCourse = this.courses.find(
    c => c.name.toLowerCase().trim() === courseName.toLowerCase().trim()
  );
  const courseId = matchedCourse ? matchedCourse.id : 0;
  const reason = this.unenrollmentReasonMap[courseName] || 'Student requested drop/unenrollment';

  this.statusMessage = '';
  this.errorMessage = '';

  this.studentService.requestUnenrollment(courseId, reason).subscribe({
    next: () => {
      this.statusMessage = `Unenrollment request for "${courseName}" sent to Admin!`;
      this.unenrollmentReasonMap[courseName] = '';
    },
    error: (err: any) => {
      this.errorMessage = typeof err.error === 'string' 
        ? err.error 
        : (err.error?.message || 'Failed to request unenrollment.');
    }
  });
}
  onRequestAccountCreation(): void {
    if (!this.accountRequestReason) return;
    this.statusMessage = '';
    this.errorMessage = '';

    this.studentService.requestAccountCreation(this.accountRequestReason).subscribe({
      next: () => {
        this.statusMessage = 'Account link request submitted to Admin successfully!';
        this.accountRequestReason = '';
      },
      error: (err: any) => {
        this.errorMessage = err.error?.message || 'Failed to submit request.';
      }
    });
  }

  onLogout(): void {
    localStorage.clear();
    this.router.navigate(['/login']);
  }
}