import { Component, OnInit, ChangeDetectorRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { Table, TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';

import { 
  LucideAngularModule, 
  LogOut, 
  Search, 
  Trash2, 
  Edit, 
  UserPlus, 
  BookPlus, 
  Users, 
  BookOpen, 
  UserCheck, 
  Check, 
  X,
  ArrowRight
} from 'lucide-angular';

import { AdminService, QueryParameters } from '../../services/admin';
import { AuthService } from '../../services/auth';
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
  | 'course-requests';

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

  activeView: AdminView = 'overview';
  
  students: any[] = [];
  courses: any[] = [];
  
  totalStudentsCount: number = 0;
  totalCoursesCount: number = 0;
  
  studentsLoading: boolean = false;
  coursesLoading: boolean = false;

  registrationRequests: PendingRequest[] = [];
  courseRequests: PendingRequest[] = [];
  
  searchedStudent: any = null;
  searchedCourse: any = null;
  selectedStudentId: number | null = null;
  selectedCourseId: number | null = null;

  studentEditForm: FormGroup;
  courseForm: FormGroup;
  
  statusMessage: string = '';
  errorMessage: string = '';

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private confirmationService: ConfirmationService,
    private fb: FormBuilder,
    private router: Router,
    private cdr: ChangeDetectorRef
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
  }

  ngOnInit(): void {
    this.refreshOverviewCounts();
    this.refreshPendingRequests();
  }

  setView(view: AdminView): void {
    this.activeView = view;
    this.statusMessage = '';
    this.errorMessage = '';
    this.searchedStudent = null;
    this.searchedCourse = null;
  }

  refreshOverviewCounts(): void {
    this.adminService.getStudents({ pageIndex: 1, pageSize: 1 }).subscribe({
      next: (res: any) => {
        this.totalStudentsCount = res?.totalCount ?? res?.TotalCount ?? (Array.isArray(res) ? res.length : 0);
        this.cdr.detectChanges();
      }
    });

    this.adminService.getCourses({ pageIndex: 1, pageSize: 1 }).subscribe({
      next: (res: any) => {
        this.totalCoursesCount = res?.totalCount ?? res?.TotalCount ?? (Array.isArray(res) ? res.length : 0);
        this.cdr.detectChanges();
      }
    });
  }

  reloadStudentTable(): void {
    if (this.dtStudents) {
      this.dtStudents.reset();
    } else {
      this.refreshOverviewCounts();
    }
  }

  reloadCourseTable(): void {
    if (this.dtCourses) {
      this.dtCourses.reset();
    } else {
      this.refreshOverviewCounts();
    }
  }

  onLazyLoadStudents(event: TableLazyLoadEvent): void {
    setTimeout(() => {
      this.studentsLoading = true;
      this.cdr.detectChanges();

      const first = event.first ?? 0;
      const rows = event.rows ?? 5;
      const pageIndex = Math.floor(first / rows) + 1;
      const pageSize = rows;

      const searchTerm = typeof event.globalFilter === 'string' ? event.globalFilter.trim() : '';
      const sortBy = typeof event.sortField === 'string' ? event.sortField : 'Name';
      const isDescending = event.sortOrder === -1;

      const queryParams: QueryParameters = { pageIndex, pageSize, searchTerm, sortBy, isDescending };

      this.adminService.getStudents(queryParams).subscribe({
        next: (res: any) => {
          if (Array.isArray(res)) {
            this.students = res;
            this.totalStudentsCount = res.length;
          } else if (res) {
            this.students = res.items || res.Items || res.data || res.Data || [];
            this.totalStudentsCount = res.totalCount ?? res.TotalCount ?? res.count ?? this.students.length;
          } else {
            this.students = [];
            this.totalStudentsCount = 0;
          }
          this.studentsLoading = false;
          this.cdr.detectChanges();
        },
        error: (err: HttpErrorResponse) => {
          this.errorMessage = extractErrorMessage(err, 'Failed to load student directory.');
          this.studentsLoading = false;
          this.cdr.detectChanges();
        }
      });
    }, 0);
  }

  onLazyLoadCourses(event: TableLazyLoadEvent): void {
    setTimeout(() => {
      this.coursesLoading = true;
      this.cdr.detectChanges();

      const first = event.first ?? 0;
      const rows = event.rows ?? 5;
      const pageIndex = Math.floor(first / rows) + 1;
      const pageSize = rows;

      const searchTerm = typeof event.globalFilter === 'string' ? event.globalFilter.trim() : '';
      const sortBy = typeof event.sortField === 'string' ? event.sortField : 'Name';
      const isDescending = event.sortOrder === -1;

      const queryParams: QueryParameters = { pageIndex, pageSize, searchTerm, sortBy, isDescending };

      this.adminService.getCourses(queryParams).subscribe({
        next: (res: any) => {
          if (Array.isArray(res)) {
            this.courses = res;
            this.totalCoursesCount = res.length;
          } else if (res) {
            this.courses = res.items || res.Items || res.data || res.Data || [];
            this.totalCoursesCount = res.totalCount ?? res.TotalCount ?? res.count ?? this.courses.length;
          } else {
            this.courses = [];
            this.totalCoursesCount = 0;
          }
          this.coursesLoading = false;
          this.cdr.detectChanges();
        },
        error: (err: HttpErrorResponse) => {
          this.errorMessage = extractErrorMessage(err, 'Failed to load course catalog.');
          this.coursesLoading = false;
          this.cdr.detectChanges();
        }
      });
    }, 0);
  }

  refreshPendingRequests(): void {
    this.adminService.getPendingRequests().subscribe({
      next: (requests: PendingRequest[]) => {
        const allReqs = requests || [];

        this.registrationRequests = allReqs.filter(r => 
          r.requestType?.toLowerCase().includes('register') || 
          r.reason?.includes('ACCOUNT_CREATION_REQUEST')
        );

        this.courseRequests = allReqs.filter(r => 
          r.requestType?.toLowerCase().includes('enroll') || 
          r.requestType?.toLowerCase().includes('unenroll')
        );

        this.cdr.detectChanges();
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
        this.adminService.deleteStudent(id).subscribe({
          next: () => {
            this.statusMessage = `Student #${id} deleted successfully.`;
            this.searchedStudent = null;
            this.refreshOverviewCounts();
            this.reloadStudentTable();
            this.setView('students-list');
          },
          error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Delete operation failed.')
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
        this.adminService.deleteCourse(id).subscribe({
          next: () => {
            this.statusMessage = `Course #${id} deleted successfully.`;
            this.searchedCourse = null;
            this.refreshOverviewCounts();
            this.reloadCourseTable();
            this.setView('courses-list');
          },
          error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Delete operation failed.')
        });
      }
    });
  }

  onSearchStudent(idInput: string): void {
    const id = parseInt(idInput, 10);
    if (!id) return;

    this.adminService.getStudentById(id).subscribe({
      next: (res) => {
        this.searchedStudent = res;
        this.errorMessage = '';
        this.cdr.detectChanges();
      },
      error: (err: HttpErrorResponse) => {
        this.searchedStudent = null;
        this.errorMessage = extractErrorMessage(err, `Student ID #${id} not found.`);
        this.cdr.detectChanges();
      }
    });
  }

  openEditStudent(student: any): void {
    this.selectedStudentId = student.id;
    this.studentEditForm.patchValue({ name: student.name, email: student.email, age: student.age });
    this.setView('student-edit');
  }

  onUpdateStudent(): void {
    if (this.studentEditForm.invalid || !this.selectedStudentId) {
      this.studentEditForm.markAllAsTouched();
      return;
    }

    this.adminService.updateStudent(this.selectedStudentId, this.studentEditForm.value).subscribe({
      next: () => {
        this.statusMessage = 'Student profile updated successfully!';
        this.refreshOverviewCounts();
        this.reloadStudentTable();
        this.setView('students-list');
      },
      error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Update failed.')
    });
  }

  onSearchCourse(idInput: string): void {
    const id = parseInt(idInput, 10);
    if (!id) return;

    this.adminService.getCourseById(id).subscribe({
      next: (res) => {
        this.searchedCourse = res;
        this.errorMessage = '';
        this.cdr.detectChanges();
      },
      error: (err: HttpErrorResponse) => {
        this.searchedCourse = null;
        this.errorMessage = extractErrorMessage(err, `Course ID #${id} not found.`);
        this.cdr.detectChanges();
      }
    });
  }

  openEditCourse(course: any): void {
    this.selectedCourseId = course.id;
    this.courseForm.patchValue({ name: course.name, credits: course.credits });
    this.setView('course-form');
  }

  onSaveCourse(): void {
    if (this.courseForm.invalid) {
      this.courseForm.markAllAsTouched();
      return;
    }

    if (this.selectedCourseId) {
      this.adminService.updateCourse(this.selectedCourseId, this.courseForm.value).subscribe({
        next: () => {
          this.statusMessage = 'Course updated successfully!';
          this.refreshOverviewCounts();
          this.reloadCourseTable();
          this.setView('courses-list');
        },
        error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Update failed.')
      });
    } else {
      this.adminService.createCourse(this.courseForm.value).subscribe({
        next: () => {
          this.statusMessage = 'New course created successfully!';
          this.refreshOverviewCounts();
          this.reloadCourseTable();
          this.setView('courses-list');
        },
        error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Creation failed.')
      });
    }
  }

  onProcessRequest(requestId: number, approve: boolean): void {
    this.adminService.processRequest(requestId, approve).subscribe({
      next: (res) => {
        this.statusMessage = res.message || (approve ? 'Approved successfully.' : 'Rejected.');
        this.refreshOverviewCounts();
        this.refreshPendingRequests();
        this.reloadStudentTable();
        this.reloadCourseTable();
      },
      error: (err: HttpErrorResponse) => this.errorMessage = extractErrorMessage(err, 'Processing failed.')
    });
  }

  onLogout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}