#!/bin/bash

prompt=$(cat ralph/prompt.md)

claude --worktree --permission-mode auto "$prompt"
