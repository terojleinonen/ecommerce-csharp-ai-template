import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/context';

function useRedirectTarget() {
  const location = useLocation();
  return (location.state as { from?: string } | null)?.from ?? '/';
}

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const target = useRedirectTarget();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setPending(true);
    setError(null);
    try {
      await login(email, password);
      navigate(target, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign in failed.');
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="auth-page">
      <form className="form card" onSubmit={onSubmit}>
        <h1>Sign in</h1>
        <label className="field">
          <span>Email</span>
          <input type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label className="field">
          <span>Password</span>
          <input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </label>
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        <button type="submit" className="btn btn--primary btn--block" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
        <p className="muted">
          New here? <Link to="/register" state={{ from: target }}>Create an account</Link>
        </p>
      </form>
    </div>
  );
}

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const target = useRedirectTarget();
  const [form, setForm] = useState({ displayName: '', email: '', password: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setPending(true);
    setError(null);
    setErrors({});
    try {
      await register(form.email, form.password, form.displayName);
      navigate(target, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.problem.errors) setErrors(err.fieldErrors);
      else setError(err instanceof Error ? err.message : 'Registration failed.');
    } finally {
      setPending(false);
    }
  };

  const field = (name: keyof typeof form, label: string, type: string, autoComplete: string) => (
    <label className="field">
      <span>{label}</span>
      <input
        type={type}
        autoComplete={autoComplete}
        required
        value={form[name]}
        onChange={(e) => setForm((f) => ({ ...f, [name]: e.target.value }))}
        aria-invalid={Boolean(errors[name])}
      />
      {errors[name] && <small className="field__error">{errors[name]}</small>}
    </label>
  );

  return (
    <div className="auth-page">
      <form className="form card" onSubmit={onSubmit} noValidate>
        <h1>Create account</h1>
        {field('displayName', 'Name', 'text', 'name')}
        {field('email', 'Email', 'email', 'email')}
        {field('password', 'Password (min. 8 characters)', 'password', 'new-password')}
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        <button type="submit" className="btn btn--primary btn--block" disabled={pending}>
          {pending ? 'Creating account…' : 'Create account'}
        </button>
        <p className="muted">
          Already have an account? <Link to="/login" state={{ from: target }}>Sign in</Link>
        </p>
      </form>
    </div>
  );
}
