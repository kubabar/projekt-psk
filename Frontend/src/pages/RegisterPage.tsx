import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { authAPI } from '../services/api';

export const RegisterPage = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const navigate = useNavigate();

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (password !== confirmPassword) {
      setError('Hasła nie są identyczne');
      return;
    }

    if (password.length < 8) {
      setError('Hasło musi mieć minimum 8 znaków');
      return;
    }

    setLoading(true);

    try {
      const response = await authAPI.register(email, password);
      
      if (response.data.success) {
        setSuccess(true);
        setTimeout(() => navigate('/login'), 2000);
      } else {
        setError(response.data.error || 'Błąd rejestracji');
      }
    } catch (err: any) {
      setError(err.response?.data?.error || 'Błąd połączenia');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <h1 style={{ marginBottom: '30px' }}>Rejestracja</h1>
      
      {error && <div className="error">{error}</div>}
      {success && <div className="success">Konto utworzone! Przekierowanie do logowania...</div>}
      
      <form onSubmit={handleRegister}>
        <div className="form-group">
          <label>Email</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={loading || success}
          />
        </div>
        
        <div className="form-group">
          <label>Hasło (min. 8 znaków)</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={8}
            required
            disabled={loading || success}
          />
        </div>
        
        <div className="form-group">
          <label>Potwierdź hasło</label>
          <input
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            disabled={loading || success}
          />
        </div>
        
        <button type="submit" disabled={loading || success}>
          {loading ? 'Rejestracja...' : 'Zarejestruj się'}
        </button>
        
        <div style={{ marginTop: '20px', textAlign: 'center' }}>
          <Link to="/login" className="link">Masz już konto? Zaloguj się</Link>
        </div>
      </form>
    </div>
  );
};
