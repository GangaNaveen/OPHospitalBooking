// PWA Authentication Handler
// Manages persistent login using IndexedDB and session storage

class PWAAuth {
  constructor() {
    this.dbName = 'HospitalOPBookingDB';
    this.storeName = 'authStore';
    this.db = null;
    this.initDB();
  }

  // Initialize IndexedDB for persistent auth storage
  initDB() {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, 1);

      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        this.db = request.result;
        resolve(this.db);
      };

      request.onupgradeneeded = (event) => {
        const db = event.target.result;
        if (!db.objectStoreNames.contains(this.storeName)) {
          db.createObjectStore(this.storeName);
        }
      };
    });
  }

  // Save auth data to IndexedDB
  async saveAuthData(email, name, role) {
    if (!this.db) await this.initDB();

    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const authData = {
        email,
        name,
        role,
        timestamp: Date.now()
      };

      const request = store.put(authData, 'user');
      request.onerror = () => reject(request.error);
      request.onsuccess = () => resolve(authData);
    });
  }

  // Retrieve auth data from IndexedDB
  async getAuthData() {
    if (!this.db) await this.initDB();

    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.get('user');

      request.onerror = () => reject(request.error);
      request.onsuccess = () => resolve(request.result || null);
    });
  }

  // Clear auth data
  async clearAuthData() {
    if (!this.db) await this.initDB();

    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.delete('user');

      request.onerror = () => reject(request.error);
      request.onsuccess = () => resolve();
    });
  }

  // Check if user is logged in
  async isLoggedIn() {
    const authData = await this.getAuthData();
    return authData !== null;
  }

  // Get current user info
  async getCurrentUser() {
    return await this.getAuthData();
  }
}

// Initialize global auth instance
const pwaAuth = new PWAAuth();

// Check persistent login on page load
document.addEventListener('DOMContentLoaded', async () => {
  // Only check on login page to redirect if already logged in
  if (window.location.pathname === '/Account/Login' || 
      window.location.pathname === '/Account/RegisterPatient' ||
      window.location.pathname === '/Account/RegisterHospital') {
    
    const isLoggedIn = await pwaAuth.isLoggedIn();
    if (isLoggedIn) {
      // Check if session is still valid (user info is visible in navbar)
      const userElement = document.querySelector('.navbar-text .text-white');
      if (!userElement || !userElement.textContent.trim()) {
        // Session expired or not loaded from server, but user was previously logged in
        // Redirect to check if server session still exists
        const response = await fetch('/Home/Index', { method: 'HEAD' });
        if (response.ok) {
          // Server session is still valid
          window.location.href = '/Home/Index';
        } else {
          // Server session expired but client has cached login
          // User needs to log in again
          console.log('Server session expired. User needs to log in again.');
        }
      } else {
        // User is logged in on this page
        window.location.href = '/Home/Index';
      }
    }
  } else {
    // On other pages, verify session and ensure IndexedDB is in sync
    const userElement = document.querySelector('.navbar-text .text-white');
    if (userElement && userElement.textContent.trim()) {
      // User is logged in - ensure IndexedDB has the latest data
      const userRole = document.querySelector('.badge.bg-light')?.textContent?.trim();
      if (userRole) {
        const userName = userElement.textContent.replace(userRole, '').trim();
        // Extract email from hidden element or other source if available
        // For now, just verify it's in sync on the next request
      }
    }
  }
});

// Store auth data when login is successful
function saveLoginData(email, name, role) {
  pwaAuth.saveAuthData(email, name, role).catch(err => {
    console.warn('Failed to save auth data:', err);
  });
}

// Clear auth data on logout
function clearLoginData() {
  pwaAuth.clearAuthData().catch(err => {
    console.warn('Failed to clear auth data:', err);
  });
}

// Add offline indicator
window.addEventListener('offline', () => {
  console.log('App is now offline');
  showOfflineIndicator();
});

window.addEventListener('online', () => {
  console.log('App is back online');
  hideOfflineIndicator();
});

function showOfflineIndicator() {
  const indicator = document.getElementById('offline-indicator');
  if (!indicator) {
    const div = document.createElement('div');
    div.id = 'offline-indicator';
    div.className = 'alert alert-warning position-fixed top-0 start-0 end-0 rounded-0 mb-0';
    div.style.zIndex = '9999';
    div.innerHTML = '<i class="bi bi-wifi-off me-2"></i>You are currently offline. Some features may be unavailable.';
    document.body.insertBefore(div, document.body.firstChild);
  } else {
    indicator.style.display = 'block';
  }
}

function hideOfflineIndicator() {
  const indicator = document.getElementById('offline-indicator');
  if (indicator) {
    indicator.style.display = 'none';
  }
}
