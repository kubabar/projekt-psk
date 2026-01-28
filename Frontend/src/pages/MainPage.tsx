import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { externalAPI } from '../services/api';
import { websocketService } from '../services/websocket';

export const MainPage = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  
  // Company search
  const [nip, setNip] = useState('');
  const [companyLoading, setCompanyLoading] = useState(false);
  const [companyResult, setCompanyResult] = useState<any>(null);
  const [companyError, setCompanyError] = useState('');
  
  // Currency rates
  const [currency, setCurrency] = useState('USD');
  const [currencyLoading, setCurrencyLoading] = useState(false);
  const [currencyResult, setCurrencyResult] = useState<any>(null);
  const [currencyError, setCurrencyError] = useState('');

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleCompanySearch = async (e: React.FormEvent) => {
    e.preventDefault();
    setCompanyError('');
    setCompanyResult(null);
    setCompanyLoading(true);

    try {
      const response = await externalAPI.getCompanyInfo(nip);
      
      if (response.data.success && response.data.data) {
        const taskId = response.data.data.taskId;
        
        websocketService.onTaskComplete(taskId, (data) => {
          setCompanyLoading(false);
          
          if (data.success) {
            setCompanyResult(data.data);
          } else {
            setCompanyError(data.error || 'Nie znaleziono firmy');
          }
        });
      } else {
        setCompanyError(response.data.error || 'Błąd');
        setCompanyLoading(false);
      }
    } catch (err: any) {
      setCompanyError(err.response?.data?.error || 'Błąd połączenia');
      setCompanyLoading(false);
    }
  };

  const handleCurrencySearch = async (e: React.FormEvent) => {
    e.preventDefault();
    setCurrencyError('');
    setCurrencyResult(null);
    setCurrencyLoading(true);

    try {
      const response = await externalAPI.getCurrencyRate(currency);
      
      if (response.data.success && response.data.data) {
        const taskId = response.data.data.taskId;
        
        websocketService.onTaskComplete(taskId, (data) => {
          setCurrencyLoading(false);
          
          if (data.success) {
            setCurrencyResult(data.data);
          } else {
            setCurrencyError(data.error || 'Nie można pobrać kursu');
          }
        });
      } else {
        setCurrencyError(response.data.error || 'Błąd');
        setCurrencyLoading(false);
      }
    } catch (err: any) {
      setCurrencyError(err.response?.data?.error || 'Błąd połączenia');
      setCurrencyLoading(false);
    }
  };

  return (
    <div>
      <div className="navbar">
        <div className="navbar-content">
          <h1>Auth System</h1>
          <div className="navbar-actions">
            <span style={{ marginRight: '20px' }}>{user?.email}</span>
            <button onClick={() => navigate('/change-password')}>Zmień hasło</button>
            <button onClick={handleLogout} className="secondary">Wyloguj</button>
          </div>
        </div>
      </div>

      <div className="container">
        {/* Company Search */}
        <div className="card">
          <h2>Wyszukaj firmę po NIP</h2>
          
          {companyError && <div className="error">{companyError}</div>}
          
          <form onSubmit={handleCompanySearch}>
            <div className="form-group">
              <label>NIP (10 cyfr)</label>
              <input
                type="text"
                value={nip}
                onChange={(e) => setNip(e.target.value.replace(/[^0-9]/g, ''))}
                maxLength={10}
                placeholder="1234567890"
                required
                disabled={companyLoading}
              />
            </div>
            
            <button type="submit" disabled={companyLoading || nip.length !== 10}>
              {companyLoading ? 'Wyszukiwanie...' : 'Szukaj'}
            </button>
          </form>

          {companyLoading && <div className="loading">Czekam na odpowiedź</div>}

          {companyResult && (
            <div className="result">
              <h3>Wyniki:</h3>
              {Array.isArray(companyResult) ? (
                <table className="result-table">
                  <thead>
                    <tr>
                      <th>Nazwa</th>
                      <th>NIP</th>
                      <th>Adres</th>
                      <th>Miejscowość</th>
                    </tr>
                  </thead>
                  <tbody>
                    {companyResult.map((company: any, idx: number) => (
                      <tr key={idx}>
                        <td>{company.Nazwa}</td>
                        <td>{company.Nip}</td>
                        <td>{company.Adres}</td>
                        <td>{company.Miejscowosc}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <pre>{JSON.stringify(companyResult, null, 2)}</pre>
              )}
            </div>
          )}
        </div>

        {/* Currency Rates */}
        <div className="card">
          <h2>Sprawdź kurs waluty</h2>
          
          {currencyError && <div className="error">{currencyError}</div>}
          
          <form onSubmit={handleCurrencySearch}>
            <div className="form-group">
              <label>Waluta</label>
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                disabled={currencyLoading}
              >
                <option value="USD">USD - Dolar amerykański</option>
                <option value="EUR">EUR - Euro</option>
                <option value="GBP">GBP - Funt brytyjski</option>
                <option value="CHF">CHF - Frank szwajcarski</option>
                <option value="JPY">JPY - Jen japoński</option>
                <option value="CZK">CZK - Korona czeska</option>
              </select>
            </div>
            
            <button type="submit" disabled={currencyLoading}>
              {currencyLoading ? 'Pobieranie...' : 'Pobierz kurs'}
            </button>
          </form>

          {currencyLoading && <div className="loading">Czekam na odpowiedź</div>}

          {currencyResult && (
            <div className="result">
              <h3>Aktualny kurs:</h3>
              {currencyResult.Rate ? (
                <div style={{ fontSize: '24px', fontWeight: 'bold', marginTop: '10px' }}>
                  1 {currencyResult.CurrencyCode} = {currencyResult.Rate} PLN
                </div>
              ) : (
                <pre>{JSON.stringify(currencyResult, null, 2)}</pre>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
