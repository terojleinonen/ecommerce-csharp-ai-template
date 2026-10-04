import { useState, type FormEvent } from 'react';
import { NavLink, Route, Routes } from 'react-router';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { api } from '../api/endpoints';
import type { OrderStatus, Product, UpsertProductRequest } from '../api/types';
import { Pagination } from '../components/Pagination';
import { ErrorState, Spinner } from '../components/States';
import { formatDate, formatPrice } from '../lib/format';
import { StatusBadge } from './OrdersPage';

export function AdminPage() {
  return (
    <div className="admin">
      <h1>Store admin</h1>
      <nav className="tabs" aria-label="Admin sections">
        <NavLink to="/admin" end>
          Products
        </NavLink>
        <NavLink to="/admin/orders">Orders</NavLink>
      </nav>
      <Routes>
        <Route index element={<AdminProducts />} />
        <Route path="orders" element={<AdminOrders />} />
      </Routes>
    </div>
  );
}

function AdminProducts() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Product | 'new' | null>(null);

  const query = { search, page, pageSize: 15, sort: 'newest' as const };
  const products = useQuery({
    queryKey: ['admin', 'products', query],
    queryFn: ({ signal }) => api.admin.products(query, signal),
    placeholderData: keepPreviousData,
  });

  const archive = useMutation({
    mutationFn: api.admin.archiveProduct,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
      queryClient.invalidateQueries({ queryKey: ['products'] });
    },
  });

  return (
    <section>
      <div className="toolbar">
        <input
          type="search"
          placeholder="Filter products…"
          aria-label="Filter products"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <button type="button" className="btn btn--primary" onClick={() => setEditing('new')}>
          + New product
        </button>
      </div>

      {products.isPending ? (
        <Spinner />
      ) : products.isError ? (
        <ErrorState error={products.error} />
      ) : (
        <>
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>SKU</th>
                  <th>Category</th>
                  <th>Price</th>
                  <th>Stock</th>
                  <th>Status</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {products.data.items.map((p) => (
                  <tr key={p.id} className={p.isActive ? '' : 'is-muted'}>
                    <td>{p.name}</td>
                    <td>
                      <code>{p.sku}</code>
                    </td>
                    <td>{p.category}</td>
                    <td>{formatPrice(p.price)}</td>
                    <td>{p.stockQuantity}</td>
                    <td>{p.isActive ? 'Active' : 'Archived'}</td>
                    <td className="table__actions">
                      <button type="button" className="link-button" onClick={() => setEditing(p)}>
                        Edit
                      </button>
                      {p.isActive && (
                        <button
                          type="button"
                          className="link-button link-button--danger"
                          disabled={archive.isPending}
                          onClick={() => archive.mutate(p.id)}
                        >
                          Archive
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={page} totalPages={products.data.totalPages} onChange={setPage} />
        </>
      )}

      {editing && (
        <ProductForm
          product={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
            queryClient.invalidateQueries({ queryKey: ['products'] });
            queryClient.invalidateQueries({ queryKey: ['product'] });
          }}
        />
      )}
    </section>
  );
}

function toForm(p: Product | null): UpsertProductRequest {
  return {
    sku: p?.sku ?? '',
    name: p?.name ?? '',
    description: p?.description ?? '',
    price: p?.price ?? 0,
    imageUrl: p?.imageUrl ?? '',
    categoryId: p?.categoryId ?? 0,
    stockQuantity: p?.stockQuantity ?? 0,
    isActive: p?.isActive ?? true,
  };
}

export function ProductForm({ product, onClose, onSaved }: { product: Product | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<UpsertProductRequest>(() => toForm(product));
  const categories = useQuery({ queryKey: ['categories'], queryFn: ({ signal }) => api.categories(signal) });

  const save = useMutation({
    mutationFn: (body: UpsertProductRequest) =>
      product ? api.admin.updateProduct(product.id, body) : api.admin.createProduct(body),
    onSuccess: onSaved,
  });

  const generate = useMutation({
    mutationFn: () =>
      api.admin.generateDescription({
        name: form.name,
        categoryId: form.categoryId,
        price: form.price,
        description: form.description || null,
      }),
    onSuccess: (res) => setForm((f) => ({ ...f, description: res.description })),
  });

  const errors = save.error instanceof ApiError ? save.error.fieldErrors : {};
  const set = <K extends keyof UpsertProductRequest>(key: K, value: UpsertProductRequest[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    save.mutate({ ...form, imageUrl: form.imageUrl || null, description: form.description || null });
  };

  return (
    <div className="modal-backdrop" role="presentation" onClick={onClose}>
      <form
        className="modal form"
        role="dialog"
        aria-modal="true"
        aria-labelledby="product-form-title"
        onClick={(e) => e.stopPropagation()}
        onSubmit={onSubmit}
        onKeyDown={(e) => e.key === 'Escape' && onClose()}
        noValidate
      >
        <h2 id="product-form-title">{product ? `Edit ${product.name}` : 'New product'}</h2>

        <div className="form-grid">
          <label className="field">
            <span>Name</span>
            <input value={form.name} onChange={(e) => set('name', e.target.value)} aria-invalid={Boolean(errors.name)} autoFocus />
            {errors.name && <small className="field__error">{errors.name}</small>}
          </label>
          <label className="field">
            <span>SKU</span>
            <input value={form.sku} onChange={(e) => set('sku', e.target.value.toUpperCase())} aria-invalid={Boolean(errors.sku)} />
            {errors.sku && <small className="field__error">{errors.sku}</small>}
          </label>
          <label className="field">
            <span>Category</span>
            <select value={form.categoryId} onChange={(e) => set('categoryId', Number(e.target.value))} aria-invalid={Boolean(errors.categoryId)}>
              <option value={0}>Select…</option>
              {categories.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {errors.categoryId && <small className="field__error">{errors.categoryId}</small>}
          </label>
          <label className="field">
            <span>Price (€)</span>
            <input type="number" step="0.01" min="0" value={form.price} onChange={(e) => set('price', Number(e.target.value))} aria-invalid={Boolean(errors.price)} />
            {errors.price && <small className="field__error">{errors.price}</small>}
          </label>
          <label className="field">
            <span>Stock</span>
            <input type="number" min="0" value={form.stockQuantity} onChange={(e) => set('stockQuantity', Number(e.target.value))} />
          </label>
          <label className="field">
            <span>Image URL (optional)</span>
            <input type="url" value={form.imageUrl ?? ''} onChange={(e) => set('imageUrl', e.target.value)} aria-invalid={Boolean(errors.imageUrl)} />
            {errors.imageUrl && <small className="field__error">{errors.imageUrl}</small>}
          </label>
        </div>

        <label className="field">
          <span className="field__label-row">
            Description
            <button
              type="button"
              className="btn btn--sm btn--ai"
              disabled={!form.name || !form.categoryId || generate.isPending}
              onClick={() => generate.mutate()}
              title={!form.name || !form.categoryId ? 'Enter a name and category first' : 'Draft copy with AI'}
            >
              {generate.isPending ? 'Writing…' : '✦ Generate with AI'}
            </button>
          </span>
          <textarea rows={7} value={form.description ?? ''} onChange={(e) => set('description', e.target.value)} />
          {generate.isError && <small className="field__error">{generate.error.message}</small>}
        </label>

        <label className="checkbox">
          <input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          Visible in store
        </label>

        {save.isError && !Object.keys(errors).length && (
          <p className="alert" role="alert">
            {save.error.message}
          </p>
        )}

        <div className="modal__actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn--primary" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save product'}
          </button>
        </div>
      </form>
    </div>
  );
}

const NEXT_STATUSES: Record<OrderStatus, OrderStatus[]> = {
  placed: ['shipped', 'cancelled'],
  shipped: ['delivered'],
  delivered: [],
  cancelled: [],
};

function AdminOrders() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const orders = useQuery({
    queryKey: ['admin', 'orders', page],
    queryFn: ({ signal }) => api.admin.orders(page, signal),
    placeholderData: keepPreviousData,
  });
  const update = useMutation({
    mutationFn: ({ id, status }: { id: string; status: OrderStatus }) => api.admin.updateOrderStatus(id, status),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'orders'] }),
  });

  if (orders.isPending) return <Spinner />;
  if (orders.isError) return <ErrorState error={orders.error} />;

  return (
    <section>
      {update.isError && (
        <p className="alert" role="alert">
          {update.error.message}
        </p>
      )}
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>Order</th>
              <th>Date</th>
              <th>Customer</th>
              <th>Items</th>
              <th>Total</th>
              <th>Status</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {orders.data.items.map((o) => (
              <tr key={o.id}>
                <td>
                  <code>{o.orderNumber}</code>
                </td>
                <td>{formatDate(o.createdAt)}</td>
                <td>{o.shippingAddress.fullName}</td>
                <td>{o.items.reduce((n, i) => n + i.quantity, 0)}</td>
                <td>{formatPrice(o.total)}</td>
                <td>
                  <StatusBadge status={o.status} />
                </td>
                <td className="table__actions">
                  {NEXT_STATUSES[o.status].map((s) => (
                    <button
                      key={s}
                      type="button"
                      className={`link-button ${s === 'cancelled' ? 'link-button--danger' : ''}`}
                      disabled={update.isPending}
                      onClick={() => update.mutate({ id: o.id, status: s })}
                    >
                      Mark {s}
                    </button>
                  ))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {orders.data.items.length === 0 && <p className="muted">No orders yet.</p>}
      <Pagination page={page} totalPages={orders.data.totalPages} onChange={setPage} />
    </section>
  );
}
