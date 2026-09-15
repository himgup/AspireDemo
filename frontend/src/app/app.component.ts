import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OrderService } from './order.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnDestroy, OnInit {
  item = signal('');
  quantity = signal(1);
  submitting = signal(false);
  apiHealthy = signal(false);
  lastUpdated = signal<Date | null>(null);
  errorMessage = signal('');
  private refreshTimer?: ReturnType<typeof setInterval>;

  constructor(public orders: OrderService) {}

  ngOnInit() {
    this.refreshData();
    this.refreshTimer = setInterval(() => this.refreshData(), 3000);
  }

  ngOnDestroy() {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  async refreshData() {
    try {
      const [healthy] = await Promise.all([this.orders.health(), this.orders.refresh()]);
      this.apiHealthy.set(healthy);
      this.lastUpdated.set(new Date());
      this.errorMessage.set('');
    } catch {
      this.apiHealthy.set(false);
      this.errorMessage.set('The Order API is not reachable yet. Check the Aspire Dashboard.');
    }
  }

  async submit() {
    if (!this.item()) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    try {
      await this.orders.place(this.item(), this.quantity());
      this.item.set('');
      this.quantity.set(1);
      await this.refreshData();
    } catch (error) {
      this.errorMessage.set(error instanceof Error ? error.message : 'The order could not be created.');
    } finally {
      this.submitting.set(false);
    }
  }
}
