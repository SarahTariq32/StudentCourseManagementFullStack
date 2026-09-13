import { Component, OnInit, signal, computed, effect, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap } from 'rxjs';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { 
  LucideAngularModule, 
  LogOut, 
  BookOpen, 
  GraduationCap, 
  User, 
  Check, 
  Send, 
  MinusCircle, 
  Edit3, 
  AlertTriangle,
  ArrowRight,
  UserCheck
} from 'lucide-angular';

import { CourseService } from '../../services/course';
import { StudentService } from '../../services/student';
import { AuthService } from '../../services/auth';
import { StudentProfile } from '../../models/student.model';
import { Course } from '../../models/course.model';
import { extractErrorMessage } from '../../utils/http-error.util';

type StudentView = 'overview' | 'my-courses' | 'available-courses' | 'profile';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    FormsModule,
    InputTextModule,
    InputNumberModule,
    ButtonModule,
    SkeletonModule,
    LucideAngularModule
  ],
  templateUrl: './student-dashboard.html',
  styleUrl: './student-dashboard.scss'
})
export class StudentDashboardComponent implements OnInit {
  private destroyRef = inject(DestroyRef);

  readonly LogOutIcon = LogOut;
  readonly BookOpenIcon = BookOpen;
  readonly GraduationCapIcon = GraduationCap;
  readonly UserIcon = User;
  readonly CheckIcon = Check;
  readonly SendIcon = Send;
  readonly MinusCircleIcon = MinusCircle;
  readonly Edit3Icon = Edit3;
  readonly AlertTriangleIcon = AlertTriangle;
  readonly ArrowRightIcon = ArrowRight;
  readonly UserCheckIcon = UserCheck;

  activeView = signal<StudentView>('overview');
  
  courses = signal<Course[]>([]);
  allSystemCourses = signal<Course[]>([]);
  studentProfile = signal<StudentProfile | null>(null);
  
  isVerified = signal<boolean>(false);
  isLoading = signal<boolean>(true);
  isEditingProfile = signal<boolean>(false);

  statusMessage = signal<string>('');
  errorMessage = signal<string>('');
  showMaxCoursesBanner = signal<boolean>(false);

  enrolledCount = computed(() => this.studentProfile()?.enrolledCourses?.length || 0);
  maxCoursesReached = computed(() => this.enrolledCount() >= 7);

  profileForm: FormGroup;
  
  accountRequestReason: string = '';
  enrollmentReasonMap: { [courseId: number]: string } = {};
  unenrollmentReasonMap: { [courseName: string]: string } = {};

  constructor(
    private courseService: CourseService,
    private studentService: StudentService,
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router
  ) {
    this.profileForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      age: [20, [Validators.required, Validators.min(16)]]
    });

    effect(() => {
      this.activeView();
      this.statusMessage.set('');
      this.errorMessage.set('');
      this.showMaxCoursesBanner.set(false);
      this.isEditingProfile.set(false);
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.checkVerificationAndLoadData();
  }

  setView(view: StudentView): void {
    this.activeView.set(view);
  }

  checkVerificationAndLoadData(): void {
    this.isLoading.set(true);

    this.studentService.getMyProfile().pipe(
      catchError(() => {
        this.isVerified.set(false);
        return of({ isVerified: false, student: null });
      }),
      switchMap((res) => {
        if (res.isVerified && res.student) {
          this.isVerified.set(true);
          this.studentProfile.set(res.student);
          this.profileForm.patchValue({
            name: res.student.name,
            email: res.student.email,
            age: res.student.age
          });
        }
        return this.courseService.getCourses();
      }),
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to load course list.'));
        return of([]);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((data: any) => {
      const fetchedCourses = Array.isArray(data) ? data : (data?.items || []);
      this.courses.set(fetchedCourses);
      this.isLoading.set(false);
    });
  }

  toggleEditProfile(): void {
    const current = this.isEditingProfile();
    this.isEditingProfile.set(!current);

    const profile = this.studentProfile();
    if (profile) {
      this.profileForm.patchValue({
        name: profile.name,
        email: profile.email,
        age: profile.age
      });
    }
  }

  onSaveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.studentService.updateMyProfile(this.profileForm.value).pipe(
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to update profile details.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res) => {
      if (res) {
        this.statusMessage.set('Profile updated successfully!');
        this.isEditingProfile.set(false);
        this.checkVerificationAndLoadData();
      }
    });
  }

  onEnrollDirectly(courseId: number): void {
    this.statusMessage.set('');
    this.errorMessage.set('');

    if (this.maxCoursesReached()) {
      this.showMaxCoursesBanner.set(true);
      return;
    }

    this.studentService.enrollDirectly(courseId).pipe(
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Enrollment failed.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res: any) => {
      if (res) {
        this.statusMessage.set(res.message || 'Successfully enrolled in course!');
        this.checkVerificationAndLoadData();
      }
    });
  }

  onRequestEnrollment(courseId: number): void {
    this.statusMessage.set('');
    this.errorMessage.set('');

    if (this.maxCoursesReached()) {
      this.showMaxCoursesBanner.set(true);
      return;
    }

    const matchedCourse = this.courses().find(c => c.id === courseId);
    const courseName = matchedCourse ? matchedCourse.name : '';
    const reason = this.enrollmentReasonMap[courseId] || 'Special enrollment permission requested';

    this.studentService.requestEnrollment(courseId, reason, this.studentProfile()?.id, courseName).pipe(
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to submit enrollment request.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res: any) => {
      if (res) {
        this.statusMessage.set(res?.message || 'Enrollment request submitted to Admin for approval.');
        this.enrollmentReasonMap[courseId] = '';
      }
    });
  }

  onRequestUnenrollment(courseName: string): void {
    this.statusMessage.set('');
    this.errorMessage.set('');
    this.showMaxCoursesBanner.set(false);

    const reason = this.unenrollmentReasonMap[courseName] || 'Student requested unenrollment';

    this.studentService.requestUnenrollment(0, reason, this.studentProfile()?.id, courseName.trim()).pipe(
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to submit unenrollment request.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res: any) => {
      if (res) {
        this.statusMessage.set(res?.message || `Unenrollment request for "${courseName}" sent to Admin!`);
        this.unenrollmentReasonMap[courseName] = '';
        this.checkVerificationAndLoadData();
      }
    });
  }

  onRequestAccountCreation(): void {
    const reason = this.accountRequestReason || 'Student requested account verification/registration';
    this.statusMessage.set('');
    this.errorMessage.set('');

    this.studentService.requestAccountCreation(reason).pipe(
      catchError((err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to submit registration request.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res: any) => {
      if (res) {
        this.statusMessage.set(res?.message || 'Student registration request submitted to Admin successfully!');
        this.accountRequestReason = '';
      }
    });
  }

  isAlreadyEnrolled(courseName: string): boolean {
    const profile = this.studentProfile();
    if (!profile?.enrolledCourses) return false;
    return profile.enrolledCourses.some(
      c => c.toLowerCase().trim() === courseName.toLowerCase().trim()
    );
  }

  onLogout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}