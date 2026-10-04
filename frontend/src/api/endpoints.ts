import { apiRequest } from './client';
import type {
  AssistantReply,
  AssistantStatus,
  AuthResponse,
  Category,
  ChatMessage,
  CreateOrderRequest,
  Order,
  OrderStatus,
  PagedResult,
  Product,
  ProductQuery,
  UpsertProductRequest,
  User,
} from './types';

export const api = {
  products: (query: ProductQuery, signal?: AbortSignal) =>
    apiRequest<PagedResult<Product>>('/api/products', { query: { ...query }, signal }),
  product: (id: number, signal?: AbortSignal) => apiRequest<Product>(`/api/products/${id}`, { signal }),
  recommendations: (id: number, signal?: AbortSignal) =>
    apiRequest<Product[]>(`/api/products/${id}/recommendations`, { signal }),
  categories: (signal?: AbortSignal) => apiRequest<Category[]>('/api/categories', { signal }),

  login: (email: string, password: string) =>
    apiRequest<AuthResponse>('/api/auth/login', { method: 'POST', body: { email, password } }),
  register: (email: string, password: string, displayName: string) =>
    apiRequest<AuthResponse>('/api/auth/register', { method: 'POST', body: { email, password, displayName } }),
  me: () => apiRequest<User>('/api/auth/me'),

  placeOrder: (request: CreateOrderRequest) => apiRequest<Order>('/api/orders', { method: 'POST', body: request }),
  myOrders: (signal?: AbortSignal) => apiRequest<Order[]>('/api/orders', { signal }),
  myOrder: (id: string, signal?: AbortSignal) => apiRequest<Order>(`/api/orders/${id}`, { signal }),

  chat: (messages: ChatMessage[], signal?: AbortSignal) =>
    apiRequest<AssistantReply>('/api/assistant/chat', { method: 'POST', body: { messages }, signal }),
  assistantStatus: () => apiRequest<AssistantStatus>('/api/assistant/status'),

  admin: {
    products: (query: ProductQuery, signal?: AbortSignal) =>
      apiRequest<PagedResult<Product>>('/api/admin/products', { query: { ...query }, signal }),
    createProduct: (body: UpsertProductRequest) =>
      apiRequest<Product>('/api/admin/products', { method: 'POST', body }),
    updateProduct: (id: number, body: UpsertProductRequest) =>
      apiRequest<Product>(`/api/admin/products/${id}`, { method: 'PUT', body }),
    archiveProduct: (id: number) => apiRequest<void>(`/api/admin/products/${id}`, { method: 'DELETE' }),
    generateDescription: (draft: { name: string; categoryId: number; price: number; description: string | null }) =>
      apiRequest<{ description: string; provider: string }>('/api/admin/ai/product-description', {
        method: 'POST',
        body: draft,
      }),
    orders: (page: number, signal?: AbortSignal) =>
      apiRequest<PagedResult<Order>>('/api/admin/orders', { query: { page, pageSize: 20 }, signal }),
    updateOrderStatus: (id: string, status: OrderStatus) =>
      apiRequest<Order>(`/api/admin/orders/${id}/status`, { method: 'PATCH', body: { status } }),
  },
};
