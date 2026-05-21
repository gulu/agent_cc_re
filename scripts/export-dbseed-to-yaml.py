"""
Export knowledge base seed data from old_src/backend/Services/DbSeed.cs to YAML files.
Parses the Add() helper calls to extract category/key/value/severity entries.
"""
import re
import json
import os
import yaml

def parse_add_calls(content):
    """Parse all Add() calls in DbSeed.cs and extract structured knowledge."""
    entries = []

    # Match Add("cat", "key", J(new[]{...}), "desc", "severity", order)
    pattern = r'''Add\("(\w+)",\s*"([^"]*)",\s*J\(new\[\]\{(.*?)\}\),?\s*"([^"]*)",?\s*"(\w+)"\s*,?\s*(\d+)?\s*\)'''
    for m in re.finditer(pattern, content, re.DOTALL):
        cat, key, vals_str, desc, sev, order = m.groups()
        vals = re.findall(r'"([^"]*)"', vals_str)
        entries.append({
            'category': cat,
            'key': key,
            'values': vals,
            'description': desc,
            'severity': sev,
            'sort_order': int(order) if order else 0,
        })

    return entries

def write_yaml(entries, output_dir):
    """Write knowledge_base.yaml and terminology.yaml."""
    os.makedirs(output_dir, exist_ok=True)

    # Group by category
    cats = {}
    for e in entries:
        cat = e['category']
        if cat not in cats:
            cats[cat] = []
        cats[cat].append(e)

    kb_path = os.path.join(output_dir, 'knowledge-base.yaml')
    with open(kb_path, 'w', encoding='utf-8') as f:
        f.write('# Agent_QC Knowledge Base\n')
        f.write('# Auto-generated from old_src/backend/Services/DbSeed.cs\n')
        f.write(f'# Categories: {len(cats)}\n')
        f.write(f'# Entries: {len(entries)}\n\n')
        yaml.dump(cats, f, allow_unicode=True, default_flow_style=False)

    print(f'Written {kb_path} — {len(cats)} categories, {len(entries)} entries')
    return kb_path

def parse_terminology(content):
    """Parse SeedTerminology Add() calls."""
    terms = []
    pattern = r'''Add\("([^"]+)",\s*"([^"]+)",\s*new\[\]\{([^}]+)\}\)'''
    for m in re.finditer(pattern, content):
        term, cat, arr_str = m.groups()
        non_std = re.findall(r'"([^"]*)"', arr_str)
        terms.append({
            'standard_term': term,
            'category': cat,
            'non_standard_terms': non_std,
        })
    return terms

def parse_rads(content):
    """Parse SeedRadsStandards Add() calls."""
    rads = []
    pattern = r'''Add\("(\w[^"]+)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)"\)'''
    for m in re.finditer(pattern, content):
        type_, grade, name, desc, risk = m.groups()
        rads.append({
            'rads_type': type_,
            'grade': grade,
            'grade_name': name,
            'description': desc,
            'malignancy_risk': risk,
        })
    return rads


if __name__ == '__main__':
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    old_src = os.path.join(project_root, 'old_src', 'backend', 'Services', 'DbSeed.cs')
    knowledge_dir = os.path.join(project_root, 'knowledge')

    with open(old_src, 'r', encoding='utf-8') as f:
        content = f.read()

    entries = parse_add_calls(content)
    write_yaml(entries, knowledge_dir)

    terms = parse_terminology(content)
    terms_path = os.path.join(knowledge_dir, 'terminology.yaml')
    with open(terms_path, 'w', encoding='utf-8') as f:
        f.write('# Agent_QC Terminology Standards\n')
        f.write('# Auto-generated from old_src/backend/Services/DbSeed.cs\n')
        f.write(f'# Entries: {len(terms)}\n\n')
        yaml.dump(terms, f, allow_unicode=True, default_flow_style=False)
    print(f'Written {terms_path} — {len(terms)} entries')

    rads = parse_rads(content)
    rads_path = os.path.join(knowledge_dir, 'rads-standards.yaml')
    with open(rads_path, 'w', encoding='utf-8') as f:
        f.write('# Agent_QC RADS Classification Standards\n')
        f.write('# Auto-generated from old_src/backend/Services/DbSeed.cs\n')
        f.write(f'# Entries: {len(rads)}\n\n')
        yaml.dump(rads, f, allow_unicode=True, default_flow_style=False)
    print(f'Written {rads_path} — {len(rads)} entries')
