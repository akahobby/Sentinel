interface SearchBoxProps {
  value: string;
  placeholder?: string;
  onChange: (value: string) => void;
}

export function SearchBox({ value, placeholder = "Search...", onChange }: SearchBoxProps) {
  return (
    <input
      className="search-box"
      value={value}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
