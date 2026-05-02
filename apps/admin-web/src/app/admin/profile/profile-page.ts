import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminService } from '../admin.service';
import type { ProfileSummary } from '../admin.types';

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly profile = signal<ProfileSummary | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly form = new FormGroup({
    nome: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.maxLength(100)(c)],
    }),
  });

  ngOnInit(): void {
    this.adminService.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.form.controls.nome.setValue(p.nome ?? '');
      },
      error: () => undefined,
    });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.saved.set(false);
    this.adminService.updateProfile({ nome: this.form.controls.nome.value }).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }
}
