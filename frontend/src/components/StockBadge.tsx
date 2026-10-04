export function StockBadge({ stock }: { stock: number }) {
  if (stock <= 0) return <span className="badge badge--danger">Out of stock</span>;
  if (stock < 5) return <span className="badge badge--warning">Only {stock} left</span>;
  return <span className="badge badge--success">In stock</span>;
}
