import { Injectable, signal } from '@angular/core';

declare global {
  interface Window { __ORDER_API_URL__?: string; }
}

export interface Order {
  id: number;
  item: string;
  quantity: number;
  status: string;
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  readonly baseUrl = window.__ORDER_API_URL__ || 'http://localhost:5100';
  orders = signal<Order[]>([]);

  async refresh() {
    const res = await fetch(`${this.baseUrl}/orders`);
    if (res.ok) this.orders.set(await res.json());
  }

  async health() {
    const res = await fetch(`${this.baseUrl}/health`);
    return res.ok;
  }

  async place(item: string, quantity: number) {
    const res = await fetch(`${this.baseUrl}/orders`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ item, quantity })
    });
    if (!res.ok) throw new Error('The Order API could not create the order.');
    await this.refresh();
  }
}
