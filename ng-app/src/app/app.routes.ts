import { Routes } from '@angular/router';
import { MyDocuments } from './documents/my-documents';
import { RecycleBin } from './recycle-bin/recycle-bin';

export const routes: Routes = [
  { path: '', component: MyDocuments },
  { path: 'recycle-bin', component: RecycleBin },
  { path: '**', redirectTo: '' },
];
