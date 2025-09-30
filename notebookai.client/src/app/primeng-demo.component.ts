import { Component } from '@angular/core';

@Component({
  selector: 'app-primeng-demo',
  template: `
    <div class="p-4">
      <p-card header="PrimeNG Demo Components" subheader="Showcasing various PrimeNG components">
        
        <div class="grid gap-4">
          <!-- Buttons Section -->
          <div class="col-12 md:col-6">
            <h3>Buttons</h3>
            <div class="flex flex-wrap gap-2">
              <p-button label="Primary" severity="primary"></p-button>
              <p-button label="Secondary" severity="secondary"></p-button>
              <p-button label="Success" severity="success"></p-button>
              <p-button label="Info" severity="info"></p-button>
              <p-button label="Warn" severity="warn"></p-button>
              <p-button label="Danger" severity="danger"></p-button>
            </div>
          </div>

          <!-- Tags Section -->
          <div class="col-12 md:col-6">
            <h3>Tags</h3>
            <div class="flex flex-wrap gap-2">
              <p-tag value="Primary"></p-tag>
              <p-tag value="Success" severity="success"></p-tag>
              <p-tag value="Info" severity="info"></p-tag>
              <p-tag value="Warn" severity="warn"></p-tag>
              <p-tag value="Danger" severity="danger"></p-tag>
            </div>
          </div>
        </div>

      </p-card>
    </div>
  `,
  standalone: false
})
export class PrimengDemoComponent {}
