#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_brace_stack.py - find exact location of brace imbalance."""
import sys

path = sys.argv[1]
text = open(path, encoding="utf-8").read()
stack = []
for i, ch in enumerate(text):
    if ch == "{":
        stack.append(i)
    elif ch == "}":
        if not stack:
            line = text[:i].count("\n") + 1
            print(f"UNMATCHED CLOSE at char {i}, line {line}")
            print(repr(text[max(0, i-80):i+80]))
            sys.exit(1)
        stack.pop()
if stack:
    for pos in stack:
        line = text[:pos].count("\n") + 1
        print(f"UNMATCHED OPEN at char {pos}, line {line}")
        print(repr(text[max(0, pos-80):pos+80]))
else:
    print("All balanced")
