## Aspire
curl -sSL https://aspire.dev/install.sh | bash

## OH-MY-POSH ##
echo Install oh-my-posh
sudo wget https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v29.9.0/posh-linux-arm64 -O /usr/local/bin/oh-my-posh
sudo chmod +x /usr/local/bin/oh-my-posh

# Initialize oh-my-posh in bash with 1_shell theme
echo 'eval "$(oh-my-posh init bash --config https://raw.githubusercontent.com/JanDeDobbeleer/oh-my-posh/refs/heads/main/themes/1_shell.omp.json)"' >> ~/.bashrc

echo DONE!