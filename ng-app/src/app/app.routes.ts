import { Routes } from '@angular/router';
import { MyDocuments } from './documents/my-documents';

export const routes: Routes = [
  { path: '', component: MyDocuments },
  { path: '**', redirectTo: '' },
];
