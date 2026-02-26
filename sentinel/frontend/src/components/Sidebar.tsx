interface SidebarItem {
  key: string;
  label: string;
}

interface SidebarProps {
  items: SidebarItem[];
  active: string;
  onChange: (key: string) => void;
}

export function Sidebar({ items, active, onChange }: SidebarProps) {
  return (
    <aside className="sidebar">
      <div className="sidebar-title">Sentinel</div>
      <nav className="sidebar-nav">
        {items.map((item) => (
          <button
            key={item.key}
            className={item.key === active ? "sidebar-item active" : "sidebar-item"}
            onClick={() => onChange(item.key)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </nav>
    </aside>
  );
}

export type { SidebarItem };
