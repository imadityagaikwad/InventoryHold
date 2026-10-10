
import { useCallback, useEffect, useState } from "react";
import "./App.css";

const API = "http://localhost:5087";

type InventoryItem = {
  productId: string;
  name: string;
  availableQuantity: number;
};

type HoldItem = {
  productId: string;
  productName: string;
  quantity: number;
};

type Hold = {
  id: string;
  status: string | number;
  createdAtUtc: string;
  expiresAtUtc: string;
  items: HoldItem[];
};

function statusLabel(status: string | number) {
  if (status === 1 || status === "Active") return "Active";
  if (status === 2 || status === "Released") return "Released";
  if (status === 3 || status === "Expired") return "Expired";
  return String(status);
}


async function request<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text();

    if (response.status === 404) {
      throw new Error("404: Resource not found.");
    }

    if (response.status === 409) {
      throw new Error(
        "409: Insufficient stock or the operation conflicts with the current state.",
      );
    }

    throw new Error(
      `${response.status}: ${body || response.statusText}`,
    );
  }

  if (response.status === 204) return undefined as T;

  return response.json() as Promise<T>;
}


function App() {
  const [inventory, setInventory] = useState<InventoryItem[]>([]);
  const [holds, setHolds] = useState<Hold[]>([]);
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [lookupId, setLookupId] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [cacheStatus, setCacheStatus] = useState("");

  const loadInventory = useCallback(async () => {
    try {
      const response = await fetch(`${API}/api/inventory`);
      if (!response.ok) throw new Error(await response.text());

      const data = (await response.json()) as InventoryItem[];
      setInventory(data);
      setCacheStatus(response.headers.get("X-Cache") ?? "");
      setProductId((current) =>
        current && data.some((item) => item.productId === current)
          ? current
          : data[0]?.productId ?? "",
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load inventory");
    }
  }, []);


useEffect(() => {
  if (!error && !notice) return;

  const timer = window.setTimeout(() => {
    setError("");
    setNotice("");
  }, 10_000);

  return () => window.clearTimeout(timer);
}, [error, notice]);


  useEffect(() => {
    void loadInventory();
  }, [loadInventory]);

  async function createHold(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setNotice("");
    setLoading(true);

    try {
      const hold = await request<Hold>("/api/holds", {
        method: "POST",
        body: JSON.stringify({
          items: [{ productId, quantity }],
        }),
      });

      setHolds((current) => [hold, ...current]);
      setNotice(`Hold ${hold.id} created successfully.`);
      setLookupId(hold.id);
      await loadInventory();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not create hold");
    } finally {
      setLoading(false);
    }
  }


async function findHold(event: React.FormEvent<HTMLFormElement>) {
  event.preventDefault();
  setError("");
  setNotice("");

  const id = lookupId.trim();

  if (!id) {
    setError("Please enter a hold ID.");
    return;
  }

  try {
    const hold = await request<Hold>(
      `/api/holds/${encodeURIComponent(id)}`,
    );

    setHolds((current) => [
      hold,
      ...current.filter((item) => item.id !== hold.id),
    ]);

    setNotice("Hold details loaded.");
  } catch (e) {
    const message =
      e instanceof Error ? e.message : "Could not find hold";

    if (message.includes("404")) {
      setError("Hold not found. Check the hold ID and try again.");
    } else {
      setError("Some error occured in searching the hold");
    }
  }
}


  async function releaseHold(hold: Hold) {
    setError("");
    setNotice("");

    try {
      await request<void>(`/api/holds/${encodeURIComponent(hold.id)}`, {
        method: "DELETE",
      });

      setHolds((current) =>
        current.map((item) =>
          item.id === hold.id ? { ...item, status: "Released" } : item,
        ),
      );
      setNotice(`Hold ${hold.id} released.`);
      await loadInventory();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not release hold");
    }
  }

  return (
    <main className="dashboard">
      <header className="topbar">
        <div className="brand-mark">IH</div>
        <div>
          <p className="eyebrow">INVENTORY OPERATIONS</p>
          <h1>Inventory Hold</h1>
        </div>
        <button className="secondary refresh" onClick={() => void loadInventory()}>
          ↻ Refresh inventory
        </button>
      </header>

      <section className="intro">
        <div>
          <p className="eyebrow">CONTROL CENTER</p>
          <h2>Inventory at a glance</h2>
          <p className="muted">
            Reserve stock temporarily, track holds, and release inventory when
            you no longer need it.
          </p>
        </div>
        <div className="stat-card">
          <span>Products</span>
          <strong>{inventory.length}</strong>
          <small>Tracked inventory items</small>
        </div>
        <div className="stat-card">
          <span>Active holds</span>
          <strong>
            {holds.filter((hold) => statusLabel(hold.status) === "Active").length}
          </strong>
          <small>Recently viewed or created</small>
        </div>
      </section>


{error && (
  <div className="alert error" role="alert">
    <span>{error}</span>
    <button
      type="button"
      className="alert-close"
      aria-label="Dismiss error notification"
      onClick={() => setError("")}
    >
      ×
    </button>
  </div>
)}

{notice && (
  <div className="alert success" role="status">
    <span>{notice}</span>
    <button
      type="button"
      className="alert-close"
      aria-label="Dismiss success notification"
      onClick={() => setNotice("")}
    >
      ×
    </button>
  </div>
)}


      <section className="content-grid">
        <article className="panel inventory-panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">STOCK OVERVIEW</p>
              <h3>Available inventory</h3>
            </div>
            {cacheStatus && (
              <span className="cache-badge">Cache: {cacheStatus}</span>
            )}
          </div>

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Product ID</th>
                  <th>Available</th>
                </tr>
              </thead>
              <tbody>
                {inventory.map((item) => (
                  <tr key={item.productId}>
                    <td className="product-name">{item.name}</td>
                    <td><code>{item.productId}</code></td>
                    <td>
                      <span className="quantity-pill">
                        {item.availableQuantity}
                      </span>
                    </td>
                  </tr>
                ))}
                {inventory.length === 0 && (
                  <tr><td colSpan={3}>No inventory items found.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </article>

        <article className="panel create-panel">
          <p className="eyebrow">NEW RESERVATION</p>
          <h3>Create inventory hold</h3>
          <p className="muted">
            Choose a product and quantity to reserve available stock.
          </p>

          <form onSubmit={createHold} className="form">
            <label htmlFor="product">Product</label>
            <select
              id="product"
              value={productId}
              onChange={(event) => setProductId(event.target.value)}
              required
              disabled={inventory.length === 0}
            >
              {inventory.map((item) => (
                <option key={item.productId} value={item.productId}>
                  {item.name} — {item.availableQuantity} available
                </option>
              ))}
            </select>

            <label htmlFor="quantity">Quantity</label>
            <input
              id="quantity"
              type="number"
              min={1}
              max={
                inventory.find((item) => item.productId === productId)
                  ?.availableQuantity ?? undefined
              }
              value={quantity}
              onChange={(event) => setQuantity(Number(event.target.value))}
              required
            />

            <button
              className="primary"
              type="submit"
              disabled={loading || !productId || quantity < 1}
            >
              {loading ? "Creating hold..." : "Create hold"}
            </button>
          </form>
          <div className="hint">
            <span className="hint-dot" />
            Stock is deducted when a hold is created.
          </div>
        </article>
      </section>

      <section className="panel holds-panel">
        <div className="panel-heading">
          <div>
            <p className="eyebrow">RESERVATION MANAGEMENT</p>
            <h3>Hold records</h3>
          </div>
          <span className="muted small">{holds.length} loaded</span>
        </div>

        <form className="lookup-form" onSubmit={findHold}>
          <input
            aria-label="Hold ID"
            placeholder="Enter a hold ID to retrieve a record"
            value={lookupId}
            onChange={(event) => setLookupId(event.target.value)}
            required
          />
          <button className="secondary" type="submit">Find hold</button>
        </form>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Hold ID</th>
                <th>Items</th>
                <th>Expires (UTC)</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {holds.map((hold) => {
                const status = statusLabel(hold.status);
                return (
                  <tr key={hold.id}>
                    <td><code className="hold-id">{hold.id}</code></td>
                    <td>
                      {hold.items.map((item) => (
                        <div key={item.productId}>
                          {item.productName} × {item.quantity}
                        </div>
                      ))}
                    </td>
                    <td>{new Date(hold.expiresAtUtc).toLocaleString()}</td>
                    <td>
                      <span className={`status status-${status.toLowerCase()}`}>
                        {status}
                      </span>
                    </td>
                    <td>
                      {status === "Active" ? (
                        <button
                          className="release-button"
                          onClick={() => void releaseHold(hold)}
                        >
                          Release
                        </button>
                      ) : <span className="muted">—</span>}
                    </td>
                  </tr>
                );
              })}
              {holds.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty-state">
                    No holds loaded yet. Create a hold or search by hold ID.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <footer>
        Inventory Hold Microservice <span>·</span> .NET · MongoDB · Redis · RabbitMQ
      </footer>
    </main>
  );
}

export default App;
