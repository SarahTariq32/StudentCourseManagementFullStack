import { Component, OnInit, ViewChild, signal, effect, computed, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, switchMap, catchError, of } from 'rxjs';
import { Table, TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { 
  LucideAngularModule, 
  LogOut, Search, Trash2, Edit, UserPlus, BookPlus, Users, BookOpen,
  UserCheck, Check, X, ArrowRight, Sparkles, RefreshCw, AlertTriangle, Clock
} from 'lucide-angular';

import { AdminService, QueryParameters } from '../../services/admin';
import { AuthService } from '../../services/auth';
import { AiCourseService, EnrollmentRequestAiSummary } from '../../services/ai-course';
import { PendingRequest } from '../../models/admin.model';
import { extractErrorMessage } from '../../utils/http-error.util';

type AdminView = 
  | 'overview' 
  | 'students-list' 
  | 'student-search' 
  | 'student-edit' 
  | 'courses-list' 
  | 'course-search'
  | 'course-form' 
  | 'registration-requests'
  | 'course-requests'
  | 'ai-summary';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    ConfirmDialogModule,
    SkeletonModule,
    LucideAngularModule
  ],
  providers: [ConfirmationService],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.scss'
})
export class AdminDashboardComponent implements OnInit {
  @ViewChild('dtStudents') dtStudents!: Table;
  @ViewChild('dtCourses') dtCourses!: Table;

  private destroyRef = inject(DestroyRef);

  readonly LogOutIcon = LogOut;
  readonly SearchIcon = Search;
  readonly TrashIcon = Trash2;
  readonly EditIcon = Edit;
  readonly UserPlusIcon = UserPlus;
  readonly BookPlusIcon = BookPlus;
  readonly UsersIcon = Users;
  readonly BookOpenIcon = BookOpen;
  readonly UserCheckIcon = UserCheck;
  readonly CheckIcon = Check;
  readonly XIcon = X;
  readonly ArrowRightIcon = ArrowRight;
  readonly SparklesIcon = Sparkles;
  readonly RefreshCwIcon = RefreshCw;
  readonly AlertTriangleIcon = AlertTriangle;
  readonly ClockIcon = Clock;

  activeView = signal<AdminView>('overview');
  
  students = signal<any[]>([]);
  courses = signal<any[]>([]);
  
  totalStudentsCount = signal<number>(0);
  totalCoursesCount = signal<number>(0);
  
  studentsLoading = signal<boolean>(false);
  coursesLoading = signal<boolean>(false);

  registrationRequests = signal<PendingRequest[]>([]);
  courseRequests = signal<PendingRequest[]>([]);

  registrationRequestsCount = computed(() => this.registrationRequests().length);
  courseRequestsCount = computed(() => this.courseRequests().length);
  totalPendingRequestsCount = computed(() => this.registrationRequestsCount() + this.courseRequestsCount());
  
  searchedStudent = signal<any>(null);
  searchedCourse = signal<any>(null);
  selectedStudentId = signal<number | null>(null);
  selectedCourseId = signal<number | null>(null);

  statusMessage = signal<string>('');
  errorMessage = signal<string>('');

  // AI Summary state
  aiSummary = signal<EnrollmentRequestAiSummary | null>(null);
  isSummaryLoading = signal<boolean>(false);
  summaryError = signal<string>('');

  // Rate limit countdown state
  rateLimitSeconds = signal<number>(0);
  private rateLimitInterval: any = null;

  studentEditForm: FormGroup;
  courseForm: FormGroup;

  studentSearchControl = new FormControl<string | number | null>('');
  courseSearchControl = new FormControl<string | number | null>('');

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private aiCourseService: AiCourseService,
    private confirmationService: ConfirmationService,
    private fb: FormBuilder,
    private router: Router
  ) {
    this.studentEditForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      age: [20, [Validators.required, Validators.min(16)]]
    });

    this.courseForm = this.fb.group({
      name: ['', Validators.required],
      credits: [3, [Validators.required, Validators.min(1), Validators.max(6)]]
    });

    effect(() => {
      const currentView = this.activeView();
      this.statusMessage.set('');
      this.errorMessage.set('');
      this.searchedStudent.set(null);
      this.searchedCourse.set(null);
      this.studentSearchControl.reset('', { emitEvent: false });
      this.courseSearchControl.reset('', { emitEvent: false });

      if (currentView === 'ai-summary') {
        if (this.summaryError() !== 'rate_limit') {
          this.summaryError.set('');
        }
        if (!this.aiSummary() && !this.isSummaryLoading()) {
          this.loadAiSummary();
        }
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.refreshOverviewCounts();
    this.refreshPendingRequests();
    this.initRxjsSearchStreams();
  }

  private initRxjsSearchStreams(): void {
    this.studentSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((val) => {
        const rawVal = val !== null && val !== undefined ? String(val).trim() : '';
        const id = parseInt(rawVal, 10);
        if (isNaN(id) || id <= 0) {
          this.searchedStudent.set(null);
          this.errorMessage.set('');
          return of(null);
        }
        return this.adminService.getStudentById(id).pipe(
          catchError((err: HttpErrorResponse) => {
            this.searchedStudent.set(null);
            this.errorMessage.set(extractErrorMessage(err, `Student ID #${id} not found.`));
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((student) => {
      if (student) {
        this.searchedStudent.set(student);
        this.errorMessage.set('');
      }
    });

    this.courseSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((val) => {
        const rawVal = val !== null && val !== undefined ? String(val).trim() : '';
        const id = parseInt(rawVal, 10);
        if (isNaN(id) || id <= 0) {
          this.searchedCourse.set(null);
          this.errorMessage.set('');
          return of(null);
        }
        return this.adminService.getCourseById(id).pipe(
          catchError((err: HttpErrorResponse) => {
            this.searchedCourse.set(null);
            this.errorMessage.set(extractErrorMessage(err, `Course ID #${id} not found.`));
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((course) => {
      if (course) {
        this.searchedCourse.set(course);
        this.errorMessage.set('');
      }
    });
  }

  setView(view: AdminView): void {
    this.activeView.set(view);
  }

  private startRateLimitCountdown(seconds: number): void {
    if (this.rateLimitInterval) clearInterval(this.rateLimitInterval);
    this.rateLimitSeconds.set(seconds);

    this.rateLimitInterval = setInterval(() => {
      const current = this.rateLimitSeconds();
      if (current <= 1) {
        clearInterval(this.rateLimitInterval);
        this.rateLimitSeconds.set(0);
      } else {
        this.rateLimitSeconds.set(current - 1);
      }
    }, 1000);
  }

  loadAiSummary(): void {
    if (this.rateLimitSeconds() > 0) return;

    this.isSummaryLoading.set(true);
    this.summaryError.set('');

    this.aiCourseService.getPendingRequestsSummary().pipe(
      catchError((err: HttpErrorResponse) => {
        this.isSummaryLoading.set(false);
        this.summaryError.set(extractErrorMessage(err, 'Failed to fetch requests summary.'));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((res) => {
      this.isSummaryLoading.set(false);
      if (res) {
        this.aiSummary.set(res);
        if (!res.isAiGenerated && res.retryAfterSeconds && res.retryAfterSeconds > 0) {
          this.startRateLimitCountdown(res.retryAfterSeconds);
        }
      }
    });
  }

  refreshOverviewCounts(): void {
    this.adminService.getStudents({ pageIndex: 1, pageSize: 1 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const count = res?.totalCount ?? res?.TotalCount ?? (Array.isArray(res) ? res.length : 0);
          this.totalStudentsCount.set(count);
        }
      });

    this.adminService.getCourses({ pageIndex: 1, pageSize: 1 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const count = res?.totalCount ?? res?.TotalCount ?? (Array.isArray(res) ? res.length : 0);
          this.totalCoursesCount.set(count);
        }
      });
  }

  reloadStudentTable(): void {
    if (this.dtStudents) this.dtStudents.reset();
    else this.refreshOverviewCounts();
  }

  reloadCourseTable(): void {
    if (this.dtCourses) this.dtCourses.reset();
    else this.refreshOverviewCounts();
  }

  onLazyLoadStudents(event: TableLazyLoadEvent): void {
    setTimeout(() => {
      this.studentsLoading.set(true);
      const first = event.first ?? 0;
      const rows = event.rows ?? 5;
      const pageIndex = Math.floor(first / rows) + 1;
      const pageSize = rows;
      const searchTerm = typeof event.globalFilter === 'string' ? event.globalFilter.trim() : '';
      const sortBy = typeof event.sortField === 'string' ? event.sortField : 'Name';
      const isDescending = event.sortOrder === -1;
      const queryParams: QueryParameters = { pageIndex, pageSize, searchTerm, sortBy, isDescending };

      this.adminService.getStudents(queryParams)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res: any) => {
            if (Array.isArray(res)) {
              this.students.set(res);
              this.totalStudentsCount.set(res.length);
            } else if (res) {
              const fetchedItems = res.items || res.Items || res.data || res.Data || [];
              this.students.set(fetchedItems);
              this.totalStudentsCount.set(res.totalCount ?? res.TotalCount ?? res.count ?? fetchedItems.length);
            } else {
              this.students.set([]);
              this.totalStudentsCount.set(0);
            }
            this.studentsLoading.set(false);
          },
          error: (err: HttpErrorResponse) => {
            this.errorMessage.set(extractErrorMessage(err, 'Failed to load student directory.'));
            this.studentsLoading.set(false);
          }
        });
    }, 0);
  }

  onLazyLoadCourses(event: TableLazyLoadEvent): void {
    setTimeout(() => {
      this.coursesLoading.set(true);
      const first = event.first ?? 0;
      const rows = event.rows ?? 5;
      const pageIndex = Math.floor(first / rows) + 1;
      const pageSize = rows;
      const searchTerm = typeof event.globalFilter === 'string' ? event.globalFilter.trim() : '';
      const sortBy = typeof event.sortField === 'string' ? event.sortField : 'Name';
      const isDescending = event.sortOrder === -1;
      const queryParams: QueryParameters = { pageIndex, pageSize, searchTerm, sortBy, isDescending };

      this.adminService.getCourses(queryParams)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res: any) => {
            if (Array.isArray(res)) {
              this.courses.set(res);
              this.totalCoursesCount.set(res.length);
            } else if (res) {
              const fetchedItems = res.items || res.Items || res.data || res.Data || [];
              this.courses.set(fetchedItems);
              this.totalCoursesCount.set(res.totalCount ?? res.TotalCount ?? res.count ?? fetchedItems.length);
            } else {
              this.courses.set([]);
              this.totalCoursesCount.set(0);
            }
            this.coursesLoading.set(false);
          },
          error: (err: HttpErrorResponse) => {
            this.errorMessage.set(extractErrorMessage(err, 'Failed to load course catalog.'));
            this.coursesLoading.set(false);
          }
        });
    }, 0);
  }

  refreshPendingRequests(): void {
    this.adminService.getPendingRequests()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (requests: PendingRequest[]) => {
          const allReqs = requests || [];
          this.registrationRequests.set(allReqs.filter(r => 
            r.requestType?.toLowerCase().includes('register') || 
            r.reason?.includes('ACCOUNT_CREATION_REQUEST')
          ));
          this.courseRequests.set(allReqs.filter(r => 
            r.requestType?.toLowerCase().includes('enroll') || 
            r.requestType?.toLowerCase().includes('unenroll')
          ));
        },
        error: (err: HttpErrorResponse) => console.error('Requests fetch error:', err)
      });
  }

  onDeleteStudent(id: number): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to permanently delete Student #${id}?`,
      header: 'Confirm Student Deletion',
      acceptLabel: 'Delete Student',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-outlined p-button-secondary p-button-sm',
      accept: () => {
        this.adminService.deleteStudent(id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.statusMessage.set(`Student #${id} deleted successfully.`);
              this.searchedStudent.set(null);
              this.refreshOverviewCounts();
              this.reloadStudentTable();
              this.setView('students-list');
            },
            error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Delete operation failed.'))
          });
      }
    });
  }

  onDeleteCourse(id: number): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to permanently delete Course #${id}?`,
      header: 'Confirm Course Deletion',
      acceptLabel: 'Delete Course',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-outlined p-button-secondary p-button-sm',
      accept: () => {
        this.adminService.deleteCourse(id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.statusMessage.set(`Course #${id} deleted successfully.`);
              this.searchedCourse.set(null);
              this.refreshOverviewCounts();
              this.reloadCourseTable();
              this.setView('courses-list');
            },
            error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Delete operation failed.'))
          });
      }
    });
  }

  openEditStudent(student: any): void {
    this.selectedStudentId.set(student.id);
    this.studentEditForm.patchValue({ name: student.name, email: student.email, age: student.age });
    this.setView('student-edit');
  }

  onUpdateStudent(): void {
    const studentId = this.selectedStudentId();
    if (this.studentEditForm.invalid || !studentId) {
      this.studentEditForm.markAllAsTouched();
      return;
    }
    this.adminService.updateStudent(studentId, this.studentEditForm.value)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.statusMessage.set('Student profile updated successfully!');
          this.refreshOverviewCounts();
          this.reloadStudentTable();
          this.setView('students-list');
        },
        error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Update failed.'))
      });
  }

  openEditCourse(course: any): void {
    this.selectedCourseId.set(course.id);
    this.courseForm.patchValue({ name: course.name, credits: course.credits });
    this.setView('course-form');
  }

  onSaveCourse(): void {
    if (this.courseForm.invalid) {
      this.courseForm.markAllAsTouched();
      return;
    }
    const courseId = this.selectedCourseId();
    if (courseId) {
      this.adminService.updateCourse(courseId, this.courseForm.value)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.statusMessage.set('Course updated successfully!');
            this.refreshOverviewCounts();
            this.reloadCourseTable();
            this.setView('courses-list');
          },
          error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Update failed.'))
        });
    } else {
      this.adminService.createCourse(this.courseForm.value)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.statusMessage.set('New course created successfully!');
            this.refreshOverviewCounts();
            this.reloadCourseTable();
            this.setView('courses-list');
          },
          error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Creation failed.'))
        });
    }
  }

  onProcessRequest(requestId: number, approve: boolean): void {
    this.adminService.processRequest(requestId, approve)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.statusMessage.set(res.message || (approve ? 'Approved successfully.' : 'Rejected.'));
          this.refreshOverviewCounts();
          this.refreshPendingRequests();
          this.reloadStudentTable();
          this.reloadCourseTable();
          if (this.aiSummary()) this.loadAiSummary();
        },
        error: (err: HttpErrorResponse) => this.errorMessage.set(extractErrorMessage(err, 'Processing failed.'))
      });
  }

  onLogout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}