import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { api } from '../api/endpoints';
import type { ProductSort } from '../api/types';
import { Pagination } from '../components/Pagination';
import { ProductCard } from '../components/ProductCard';
import { EmptyState, ErrorState, Spinner } from '../components/States';

const SORTS: { value: ProductSort; label: string }[] = [
  { value: 'relevance', label: 'Featured' },
  { value: 'newest', label: 'Newest' },
  { value: 'priceAsc', label: 'Price: low to high' },
  { value: 'priceDesc', label: 'Price: high to low' },
  { value: 'name', label: 'Name' },
];

export function CatalogPage() {
  const [params, setParams] = useSearchParams();
  const search = params.get('search') ?? '';
  const category = params.get('category') ?? '';
  const sort = (params.get('sort') as ProductSort | null) ?? 'relevance';
  const inStock = params.get('inStock') === 'true';
  const page = Number(params.get('page') ?? '1') || 1;

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(changes)) {
      if (v === null || v === '') next.delete(k);
      else next.set(k, v);
    }
    if (!('page' in changes)) next.delete('page');
    setParams(next);
  };

  const categories = useQuery({ queryKey: ['categories'], queryFn: ({ signal }) => api.categories(signal) });
  const query = { search, category, sort, inStock: inStock || undefined, page, pageSize: 12 };
  const products = useQuery({
    queryKey: ['products', query],
    queryFn: ({ signal }) => api.products(query, signal),
    placeholderData: keepPreviousData,
  });

  return (
    <div className="catalog">
      <section className="hero">
        <h1>{search ? `Results for “${search}”` : 'Gear that keeps up with you'}</h1>
        <p>
          {search
            ? `${products.data?.totalCount ?? '…'} products found`
            : 'Not sure what you need? Ask the AI shopping assistant in the corner.'}
        </p>
      </section>

      <div className="toolbar">
        <div className="chips" role="group" aria-label="Categories">
          <button type="button" className={`chip ${!category ? 'chip--active' : ''}`} onClick={() => update({ category: null })}>
            All
          </button>
          {categories.data?.map((c) => (
            <button
              key={c.id}
              type="button"
              className={`chip ${category === c.slug ? 'chip--active' : ''}`}
              onClick={() => update({ category: c.slug })}
            >
              {c.name} <span className="chip__count">{c.productCount}</span>
            </button>
          ))}
        </div>
        <div className="toolbar__filters">
          <label className="checkbox">
            <input type="checkbox" checked={inStock} onChange={(e) => update({ inStock: e.target.checked ? 'true' : null })} />
            In stock only
          </label>
          <label>
            <span className="sr-only">Sort by</span>
            <select value={sort} onChange={(e) => update({ sort: e.target.value })}>
              {SORTS.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {products.isPending ? (
        <Spinner label="Loading products…" />
      ) : products.isError ? (
        <ErrorState error={products.error} onRetry={() => products.refetch()} />
      ) : products.data.items.length === 0 ? (
        <EmptyState title="No products found">
          <p>Try a different search, or ask the assistant for ideas.</p>
        </EmptyState>
      ) : (
        <>
          <div className={`product-grid ${products.isPlaceholderData ? 'is-stale' : ''}`}>
            {products.data.items.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
          <Pagination
            page={products.data.page}
            totalPages={products.data.totalPages}
            onChange={(p) => update({ page: String(p) })}
          />
        </>
      )}
    </div>
  );
}
