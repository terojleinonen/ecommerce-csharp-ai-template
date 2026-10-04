export interface Product {
  id: number;
  sku: string;
  name: string;
  slug: string;
  description: string | null;
  price: number;
  imageUrl: string | null;
  categoryId: number;
  category: string;
  stockQuantity: number;
  isActive: boolean;
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  productCount: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export type ProductSort = 'relevance' | 'newest' | 'priceAsc' | 'priceDesc' | 'name';

export interface ProductQuery {
  search?: string;
  category?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  sort?: ProductSort;
  page?: number;
  pageSize?: number;
}

export type UserRole = 'customer' | 'admin';

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export type OrderStatus = 'placed' | 'shipped' | 'delivered' | 'cancelled';

export interface ShippingAddress {
  fullName: string;
  line1: string;
  line2?: string | null;
  postalCode: string;
  city: string;
  country: string;
}

export interface OrderItem {
  productId: number;
  productName: string;
  sku: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  items: OrderItem[];
  shippingAddress: ShippingAddress;
  subtotal: number;
  shippingCost: number;
  total: number;
  createdAt: string;
}

export interface CreateOrderRequest {
  items: { productId: number; quantity: number }[];
  shippingAddress: ShippingAddress;
}

export interface UpsertProductRequest {
  sku: string;
  name: string;
  description: string | null;
  price: number;
  imageUrl: string | null;
  categoryId: number;
  stockQuantity: number;
  isActive: boolean;
}

export type ChatRole = 'user' | 'assistant';

export interface ChatMessage {
  role: ChatRole;
  content: string;
}

export interface AssistantReply {
  reply: string;
  products: Product[];
  provider: 'claude' | 'offline';
}

export interface AssistantStatus {
  provider: 'claude' | 'offline';
  llmEnabled: boolean;
  model: string | null;
}
